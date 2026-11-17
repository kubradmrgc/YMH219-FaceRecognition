namespace FaceRecognition.Models;

/// <summary>Modeldeki sayısal etiket numarasının kişi adına karşılığı.</summary>
public sealed record ModelLabel(int Id, string Name, int SampleCount);

/// <summary>
/// Eğitilmiş modelin özeti; labels.json dosyasında saklanır.
/// </summary>
public sealed record ModelInfo(IReadOnlyList<ModelLabel> Labels, DateTime TrainedAt, int SkippedSamples = 0)
{
    public int LabelCount => Labels.Count;

    public int SampleCount => Labels.Sum(l => l.SampleCount);
}
