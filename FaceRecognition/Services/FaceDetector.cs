using OpenCvSharp;
using Size = OpenCvSharp.Size;

namespace FaceRecognition.Services;

public sealed class FaceDetector : IDisposable
{
    public const double ScaleFactor = 1.1;
    public const int MinNeighbors = 5;
    public const int MinFaceSize = 60;
    public const string CascadeFileName = "haarcascade_frontalface_default.xml";

    private readonly CascadeClassifier classifier;
    private readonly object sync = new();

    public static string DefaultCascadePath => Path.Combine(AppContext.BaseDirectory, "Assets", CascadeFileName);

    public FaceDetector(string cascadePath)
    {
        if (!File.Exists(cascadePath))
        {
            throw new FaceAppException(
                $"Yüz algılama modeli bulunamadı.\n{cascadePath}\n\n" +
                $"'{CascadeFileName}' dosyası Assets klasöründe olmalı. Projeyi yeniden derleyin veya dosyayı geri yükleyin.");
        }

        string xml;
        try
        {
            xml = File.ReadAllText(cascadePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new FaceAppException($"Yüz algılama modeli okunamadı.\n{cascadePath}\n{ex.Message}", ex);
        }

        // Dosya yolu yerine içerik verilir; OpenCV'nin dosya açma fonksiyonları Unicode yollarda başarısız olabiliyor.
        classifier = new CascadeClassifier();
        bool loaded;
        try
        {
            using var storage = new FileStorage(xml, FileStorage.Modes.Read | FileStorage.Modes.Memory);
            using FileNode? node = storage.GetFirstTopLevelNode();
            loaded = node is not null && classifier.Read(node) && !classifier.Empty();
        }
        catch (OpenCVException)
        {
            loaded = false;
        }

        if (!loaded)
        {
            classifier.Dispose();
            throw new FaceAppException(
                $"Yüz algılama modeli bozuk veya geçersiz.\n{cascadePath}\n\n" +
                $"OpenCV'nin özgün '{CascadeFileName}' dosyasını Assets klasörüne geri koyun.");
        }
    }

    public Rect[] Detect(Mat image)
    {
        using var gray = new Mat();
        if (image.Channels() == 1)
        {
            image.CopyTo(gray);
        }
        else
        {
            Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);
        }
        Cv2.EqualizeHist(gray, gray);

        Rect[] faces;
        lock (sync)
        {
            faces = classifier.DetectMultiScale(gray, ScaleFactor, MinNeighbors, HaarDetectionTypes.ScaleImage, new Size(MinFaceSize, MinFaceSize));
        }
        return faces.OrderBy(f => f.X).ToArray();
    }

    public void Dispose() => classifier.Dispose();
}
