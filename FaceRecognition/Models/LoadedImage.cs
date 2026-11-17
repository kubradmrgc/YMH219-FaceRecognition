using OpenCvSharp;
using Size = OpenCvSharp.Size;

namespace FaceRecognition.Models;

/// <summary>
/// Dosyadan okunan resim. Resim çok büyükse <see cref="Image"/> küçültülmüş kopyadır.
/// </summary>
public sealed class LoadedImage : IDisposable
{
    public LoadedImage(Mat image, Size originalSize)
    {
        Image = image;
        OriginalSize = originalSize;
    }

    public Mat Image { get; }

    public Size OriginalSize { get; }

    public bool WasResized => Image.Width != OriginalSize.Width || Image.Height != OriginalSize.Height;

    public void Dispose() => Image.Dispose();
}
