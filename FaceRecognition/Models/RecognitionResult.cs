using FaceRecognition.Services;

namespace FaceRecognition.Models;

/// <summary>
/// Bir yüz için tanıma sonucu. LBPH mesafe döndürür: küçük mesafe daha benzer demektir.
/// </summary>
/// <param name="Label">Modelin en yakın bulduğu etiket.</param>
/// <param name="Distance">Ham LBPH mesafesi (histogramlar arası ki-kare uzaklığı).</param>
/// <param name="IsKnown">Mesafe eşiğin altındaysa true; değilse yüz "Bilinmeyen" sayılır.</param>
public sealed record RecognitionResult(string Label, double Distance, bool IsKnown)
{
    /// <summary>Sezgisel güven yüzdesi: 100 − mesafe (0'ın altına inmez). İstatistiksel bir olasılık değildir.</summary>
    public double Confidence => Math.Max(0, 100 - Distance);

    public string DisplayLabel => IsKnown ? Label : FaceDataset.UnknownLabel;
}
