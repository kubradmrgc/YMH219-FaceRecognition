using OpenCvSharp;

namespace FaceRecognition.Models;

public sealed record FaceDetection(Rect Bounds, RecognitionResult? Recognition = null)
{
    public string Caption(int number) => Recognition is null
        ? $"Yüz {number}"
        : $"{Recognition.DisplayLabel} (%{Recognition.Confidence:0})";
}
