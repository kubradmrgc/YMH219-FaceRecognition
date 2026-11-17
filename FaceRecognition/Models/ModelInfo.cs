namespace FaceRecognition.Models;

public sealed record ModelLabel(int Id, string Name, int SampleCount);

public sealed record ModelInfo(IReadOnlyList<ModelLabel> Labels, DateTime TrainedAt, int SkippedSamples = 0)
{
    public int LabelCount => Labels.Count;

    public int SampleCount => Labels.Sum(l => l.SampleCount);
}
