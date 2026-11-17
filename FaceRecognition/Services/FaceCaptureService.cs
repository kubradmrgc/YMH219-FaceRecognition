using System.Globalization;
using FaceRecognition.Models;
using OpenCvSharp;

namespace FaceRecognition.Services;

/// <summary>
/// Gösterilen karedeki yüzleri kırpıp kayıt klasörüne PNG olarak saklar:
/// yuz-yyyyMMdd-HHmmss-fff-N[-etiket].png
/// </summary>
public sealed class FaceCaptureService
{
    public FaceCaptureService(string directory)
    {
        DirectoryPath = directory;
    }

    public string DirectoryPath { get; }

    /// <summary>Her yüzü ayrı dosyaya kaydeder ve oluşturulan dosyaların yollarını döndürür.</summary>
    public IReadOnlyList<string> SaveFaces(Mat image, IReadOnlyList<FaceDetection> faces, DateTime time)
    {
        if (image.Empty())
        {
            throw new FaceAppException("Kaydedilecek görüntü yok. Önce kamerayı başlatın veya bir resim açın.");
        }
        if (faces.Count == 0)
        {
            throw new FaceAppException("Görüntüde kaydedilecek yüz yok.");
        }

        string stamp = time.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
        var saved = new List<string>(faces.Count);
        for (int i = 0; i < faces.Count; i++)
        {
            Rect area = FacePreprocessor.ClampToImage(faces[i].Bounds, image);
            if (area.Width <= 0 || area.Height <= 0)
            {
                continue;
            }

            string label = faces[i].Recognition is { } r ? "-" + ToFilePart(r.DisplayLabel) : "";
            string baseName = $"yuz-{stamp}-{i + 1}{label}";
            string path = Path.Combine(DirectoryPath, baseName + ".png");
            for (int n = 1; File.Exists(path); n++)
            {
                path = Path.Combine(DirectoryPath, $"{baseName}-{n}.png");
            }

            using var crop = new Mat(image, area);
            ImageFileService.WritePng(path, crop);
            saved.Add(path);
        }
        return saved;
    }

    /// <summary>Etiketteki boşlukları ve dosya adında kullanılamayan karakterleri '_' yapar.</summary>
    private static string ToFilePart(string label)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        return new string(label.Select(c => char.IsWhiteSpace(c) || invalid.Contains(c) ? '_' : c).ToArray());
    }
}
