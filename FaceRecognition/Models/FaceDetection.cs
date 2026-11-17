using OpenCvSharp;

namespace FaceRecognition.Models;

/// <summary>
/// Görüntüde bulunan bir yüzün konumu.
/// </summary>
public sealed record FaceDetection(Rect Bounds)
{
    public string Caption(int number) => $"Yüz {number}";
}
