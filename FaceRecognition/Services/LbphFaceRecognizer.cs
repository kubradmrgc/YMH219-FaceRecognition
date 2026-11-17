using OpenCvSharp;
using OpenCvSharp.Face;

namespace FaceRecognition.Services;

/// <summary>
/// OpenCV face modülündeki LBPH (Local Binary Patterns Histograms) tanıyıcısı.
/// Her yüz 8x8 hücreye bölünür, her hücrenin LBP histogramı çıkarılır; tahminde en yakın histogram bulunur.
/// </summary>
public sealed class LbphFaceRecognizer : IFaceRecognizer
{
    public const int Radius = 1;
    public const int Neighbors = 8;
    public const int GridX = 8;
    public const int GridY = 8;

    private readonly LBPHFaceRecognizer model = LBPHFaceRecognizer.Create(Radius, Neighbors, GridX, GridY);

    public void Train(IReadOnlyList<Mat> faces, IReadOnlyList<int> labels) => model.Train(faces, labels);

    public (int Label, double Distance) Predict(Mat face)
    {
        model.Predict(face, out int label, out double distance);
        return (label, distance);
    }

    public IReadOnlySet<int> GetTrainedLabels()
    {
        using Mat labels = model.GetLabels();
        var result = new HashSet<int>();
        int rows = labels.Rows;
        for (int i = 0; i < rows; i++)
        {
            result.Add(labels.Get<int>(i, 0));
        }
        return result;
    }

    // Dosya yolu yerine bellek kullanılır; OpenCV'nin dosya fonksiyonları Unicode yollarda başarısız olabiliyor.
    public string Serialize()
    {
        using var storage = new FileStorage(".yml", FileStorage.Modes.Write | FileStorage.Modes.Memory);
        model.Write(storage);
        return storage.ReleaseAndGetString();
    }

    public void Deserialize(string content)
    {
        using var storage = new FileStorage(content, FileStorage.Modes.Read | FileStorage.Modes.Memory);
        using FileNode root = storage.Root() ?? throw new InvalidDataException("Model içeriği boş.");
        model.Read(root);
    }

    public void Dispose() => model.Dispose();
}
