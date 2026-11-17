using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using FaceRecognition.Models;
using OpenCvSharp;

namespace FaceRecognition.Services;

/// <summary>
/// Tanıma modelini eğitir, diske kaydeder, açılışta yükler ve yüzler için tahmin yapar.
/// Model dosyası (lbph-model.yml) etiketleri sayı olarak tutar; sayıların kişi adları labels.json dosyasındadır.
/// </summary>
public sealed class FaceRecognizerService : IDisposable
{
    public const double DefaultThreshold = 75;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly AppPaths paths;
    private readonly Func<IFaceRecognizer> createRecognizer;
    private readonly object sync = new();
    private IFaceRecognizer? recognizer;
    private Dictionary<int, string> names = new();

    public FaceRecognizerService(AppPaths paths) : this(paths, () => new LbphFaceRecognizer())
    {
    }

    public FaceRecognizerService(AppPaths paths, Func<IFaceRecognizer> createRecognizer)
    {
        this.paths = paths;
        this.createRecognizer = createRecognizer;
    }

    /// <summary>Bu mesafenin üstündeki tahminler "Bilinmeyen" sayılır.</summary>
    public double Threshold { get; set; } = DefaultThreshold;

    public ModelInfo? Info { get; private set; }

    public bool IsTrained
    {
        get
        {
            lock (sync)
            {
                return recognizer is not null;
            }
        }
    }

    /// <summary>Veri setindeki tüm örneklerle yeni model eğitir ve kaydeder. Uzun sürebilir; arka planda çağrılmalıdır.</summary>
    public ModelInfo Train(FaceDataset dataset)
    {
        List<LabelInfo> labels = dataset.GetLabels().Where(l => l.SampleCount > 0).ToList();
        EnsureEnoughLabels(labels.Count);

        var faces = new List<Mat>();
        var ids = new List<int>();
        var modelLabels = new List<ModelLabel>();
        int skipped = 0;
        IFaceRecognizer? trained = null;
        try
        {
            foreach (LabelInfo label in labels)
            {
                int id = modelLabels.Count;
                int used = 0;
                foreach (string file in dataset.GetSampleFiles(label.Name))
                {
                    try
                    {
                        using Mat image = ImageFileService.Read(file);
                        faces.Add(FacePreprocessor.Prepare(image));
                        ids.Add(id);
                        used++;
                    }
                    catch (FaceAppException)
                    {
                        skipped++;
                    }
                }
                if (used > 0)
                {
                    modelLabels.Add(new ModelLabel(id, label.Name, used));
                }
            }
            EnsureEnoughLabels(modelLabels.Count);

            trained = createRecognizer();
            trained.Train(faces, ids);
            var info = new ModelInfo(modelLabels, DateTime.Now, skipped);
            Save(trained, info);
            Activate(trained, info);
            trained = null;
            return info;
        }
        catch (OpenCVException ex)
        {
            throw new FaceAppException($"Model eğitilemedi: {ex.Message}", ex);
        }
        finally
        {
            trained?.Dispose();
            foreach (Mat face in faces)
            {
                face.Dispose();
            }
        }
    }

    /// <summary>Kayıtlı modeli yükler. Model dosyası yoksa false döner; bozuk veya uyumsuzsa <see cref="FaceAppException"/> fırlatır.</summary>
    public bool LoadIfExists()
    {
        if (!File.Exists(paths.ModelFile))
        {
            return false;
        }
        if (!File.Exists(paths.LabelsFile))
        {
            throw new FaceAppException($"Etiket dosyası (labels.json) bulunamadı, modeli yeniden eğitin.\n{paths.LabelsFile}\n\nModel, kişi adlarıyla eşleştirilemiyor.");
        }

        string modelText;
        string labelsText;
        try
        {
            modelText = File.ReadAllText(paths.ModelFile);
            labelsText = File.ReadAllText(paths.LabelsFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new FaceAppException($"Model dosyaları okunamadı.\n{ex.Message}", ex);
        }

        ModelInfo info = ParseLabels(labelsText);
        IFaceRecognizer loaded = createRecognizer();
        try
        {
            IReadOnlySet<int> trainedLabels;
            try
            {
                loaded.Deserialize(modelText);
                trainedLabels = loaded.GetTrainedLabels();
            }
            catch (Exception ex) when (ex is OpenCVException or InvalidDataException)
            {
                throw new FaceAppException($"Model dosyası bozuk, modeli yeniden eğitin.\n{paths.ModelFile}", ex);
            }

            if (trainedLabels.Count == 0)
            {
                throw new FaceAppException($"Model dosyası bozuk veya boş, modeli yeniden eğitin.\n{paths.ModelFile}");
            }
            if (!trainedLabels.SetEquals(info.Labels.Select(l => l.Id)))
            {
                throw new FaceAppException(
                    $"labels.json ile model uyumsuz, modeli yeniden eğitin.\nModelde {trainedLabels.Count} etiket, labels.json dosyasında {info.LabelCount} etiket var.");
            }

            Activate(loaded, info);
            return true;
        }
        catch
        {
            loaded.Dispose();
            throw;
        }
    }

    /// <summary>Ön işlenmiş yüz için en yakın etiketi bulur ve eşiğe göre tanınıp tanınmadığına karar verir.</summary>
    public RecognitionResult Predict(Mat preparedFace)
    {
        lock (sync)
        {
            if (recognizer is null)
            {
                throw new FaceAppException("Model bulunamadı, önce eğitin.");
            }

            (int id, double distance) = recognizer.Predict(preparedFace);
            bool found = names.TryGetValue(id, out string? name);
            return new RecognitionResult(name ?? FaceDataset.UnknownLabel, distance, found && distance <= Threshold);
        }
    }

    private static void EnsureEnoughLabels(int count)
    {
        if (count < FaceDataset.MinLabelsForTraining)
        {
            throw new FaceAppException(
                $"Eğitim için örneği olan en az {FaceDataset.MinLabelsForTraining} etiket gerekir (şu an {count}).\n" +
                "Veri Ekle / Eğitim sekmesinden ikinci bir kişi (veya kendi yüzünüzün farklı koşulları için ikinci bir etiket) ekleyin.");
        }
    }

    private void Activate(IFaceRecognizer newRecognizer, ModelInfo info)
    {
        IFaceRecognizer? old;
        lock (sync)
        {
            old = recognizer;
            recognizer = newRecognizer;
            names = info.Labels.ToDictionary(l => l.Id, l => l.Name);
            Info = info;
        }
        old?.Dispose();
    }

    private void Save(IFaceRecognizer model, ModelInfo info)
    {
        try
        {
            Directory.CreateDirectory(paths.ModelDirectory);
            WriteAtomically(paths.ModelFile, model.Serialize());
            WriteAtomically(paths.LabelsFile, JsonSerializer.Serialize(info, JsonOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new FaceAppException($"Model kaydedilemedi.\n{paths.ModelDirectory}\n{ex.Message}", ex);
        }
    }

    /// <summary>Önce geçici dosyaya yazar, sonra yerine taşır; yazma yarıda kalırsa eski dosya bozulmaz.</summary>
    private static void WriteAtomically(string path, string content)
    {
        string temp = path + ".tmp";
        File.WriteAllText(temp, content, new UTF8Encoding(false));
        File.Move(temp, path, overwrite: true);
    }

    private ModelInfo ParseLabels(string json)
    {
        ModelInfo? info;
        try
        {
            info = JsonSerializer.Deserialize<ModelInfo>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new FaceAppException($"Etiket dosyası (labels.json) bozuk, modeli yeniden eğitin.\n{paths.LabelsFile}", ex);
        }

        if (info?.Labels is null || info.Labels.Count == 0 || info.Labels.Any(l => string.IsNullOrWhiteSpace(l.Name))
            || info.Labels.Select(l => l.Id).Distinct().Count() != info.Labels.Count)
        {
            throw new FaceAppException($"Etiket dosyası (labels.json) geçersiz, modeli yeniden eğitin.\n{paths.LabelsFile}");
        }
        return info;
    }

    public void Dispose()
    {
        lock (sync)
        {
            recognizer?.Dispose();
            recognizer = null;
        }
    }
}
