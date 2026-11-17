namespace FaceRecognition.Models;

public sealed record LabelInfo(string Name, int SampleCount)
{
    public override string ToString() => $"{Name} ({SampleCount} örnek)";
}
