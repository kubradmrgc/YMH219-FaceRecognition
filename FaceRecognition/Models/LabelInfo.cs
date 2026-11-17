namespace FaceRecognition.Models;

/// <summary>
/// Veri setindeki bir etiket (kişi) ve o etikete ait örnek yüz sayısı.
/// </summary>
public sealed record LabelInfo(string Name, int SampleCount)
{
    public override string ToString() => $"{Name} ({SampleCount} örnek)";
}
