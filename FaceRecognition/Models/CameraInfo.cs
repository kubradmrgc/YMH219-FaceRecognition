namespace FaceRecognition.Models;

public sealed record CameraInfo(int Index)
{
    public override string ToString() => Index == 0 ? "Kamera 0 (varsayılan)" : $"Kamera {Index}";
}
