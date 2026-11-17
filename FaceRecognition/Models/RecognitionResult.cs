using FaceRecognition.Services;

namespace FaceRecognition.Models;

public sealed record RecognitionResult(string Label, double Distance, bool IsKnown)
{
    public double Confidence => Math.Max(0, 100 - Distance);

    public string DisplayLabel => IsKnown ? Label : FaceDataset.UnknownLabel;
}
