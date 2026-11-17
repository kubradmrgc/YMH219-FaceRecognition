namespace FaceRecognition.Models;

/// <summary>
/// Kamera seçim listesindeki bir öğe. OpenCV kameraları ad yerine sıra numarasıyla (0, 1, 2...) açar.
/// </summary>
public sealed record CameraInfo(int Index)
{
    public override string ToString() => Index == 0 ? "Kamera 0 (varsayılan)" : $"Kamera {Index}";
}
