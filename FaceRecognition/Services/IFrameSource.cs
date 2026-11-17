using OpenCvSharp;

namespace FaceRecognition.Services;

/// <summary>
/// Kare kare görüntü veren kaynak (gerçek kamera veya test için sahte kaynak).
/// </summary>
public interface IFrameSource : IDisposable
{
    /// <summary>Sıradaki kareyi <paramref name="frame"/> içine okur; kare yoksa false döner.</summary>
    bool Read(Mat frame);
}
