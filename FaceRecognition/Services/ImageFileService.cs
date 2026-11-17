using FaceRecognition.Models;
using OpenCvSharp;
using Size = OpenCvSharp.Size;

namespace FaceRecognition.Services;

public static class ImageFileService
{
    public const int MaxImageSide = 1920;

    public const long MaxFileBytes = 50L * 1024 * 1024;

    public const string OpenFileFilter = "Resim dosyaları (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp";

    public static readonly IReadOnlyList<string> SupportedExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp" };

    public static bool IsSupported(string path) =>
        SupportedExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());

    public static LoadedImage Load(string path, int maxSide = MaxImageSide)
    {
        Mat image = Read(path);
        var originalSize = new Size(image.Width, image.Height);
        int longest = Math.Max(image.Width, image.Height);
        if (longest <= maxSide)
        {
            return new LoadedImage(image, originalSize);
        }

        double scale = (double)maxSide / longest;
        var resized = new Mat();
        Cv2.Resize(image, resized, new Size(), scale, scale, InterpolationFlags.Area);
        image.Dispose();
        return new LoadedImage(resized, originalSize);
    }

    public static Mat Read(string path)
    {
        string name = Path.GetFileName(path);
        if (!IsSupported(path))
        {
            throw new FaceAppException(
                $"'{name}' desteklenmeyen bir biçimde.\nDesteklenen biçimler: {string.Join(", ", SupportedExtensions)}");
        }

        byte[] bytes;
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                throw new FaceAppException($"Resim dosyası bulunamadı.\n{path}");
            }
            if (info.Length > MaxFileBytes)
            {
                throw new FaceAppException($"'{name}' çok büyük ({info.Length / (1024 * 1024)} MB). En fazla {MaxFileBytes / (1024 * 1024)} MB'lık resimler açılabilir.");
            }
            bytes = File.ReadAllBytes(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new FaceAppException($"'{name}' okunamadı: {ex.Message}", ex);
        }

        if (bytes.Length == 0)
        {
            throw new FaceAppException($"'{name}' geçerli bir resim değil (dosya boş).");
        }

        Mat image;
        try
        {
            image = Cv2.ImDecode(bytes, ImreadModes.Color);
        }
        catch (Exception ex) when (ex is OpenCVException or ArgumentException)
        {
            throw new FaceAppException($"'{name}' geçerli bir resim değil (dosya bozuk olabilir).", ex);
        }

        if (image.Empty())
        {
            image.Dispose();
            throw new FaceAppException($"'{name}' geçerli bir resim değil (dosya bozuk olabilir).");
        }
        return image;
    }

    public static void WritePng(string path, Mat image)
    {
        if (image.Empty())
        {
            throw new FaceAppException("Kaydedilecek resim boş.");
        }

        if (!Cv2.ImEncode(".png", image, out byte[] buffer))
        {
            throw new FaceAppException("Resim PNG biçimine dönüştürülemedi.");
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllBytes(path, buffer);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new FaceAppException($"Resim kaydedilemedi.\n{path}\n{ex.Message}", ex);
        }
    }
}
