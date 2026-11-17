using OpenCvSharp;

namespace FaceRecognition.Services;

/// <summary>
/// Ön işlenmiş (100x100 gri) yüz görüntülerinden öğrenen ve tahmin yapan tanıyıcı.
/// </summary>
public interface IFaceRecognizer : IDisposable
{
    void Train(IReadOnlyList<Mat> faces, IReadOnlyList<int> labels);

    /// <summary>En yakın etiketi ve mesafesini döndürür (küçük mesafe = daha benzer).</summary>
    (int Label, double Distance) Predict(Mat face);

    /// <summary>Modelin içinde bulunan farklı etiket numaraları.</summary>
    IReadOnlySet<int> GetTrainedLabels();

    /// <summary>Modeli metin (YAML) olarak döndürür.</summary>
    string Serialize();

    /// <summary>Modeli <see cref="Serialize"/> çıktısından yükler.</summary>
    void Deserialize(string content);
}
