using OpenCvSharp;
using Size = OpenCvSharp.Size;

namespace FaceRecognition.Services;

/// <summary>
/// Yüz görüntüsünü tanıma için standart biçime getirir: gri ton, 100x100 piksel, histogram eşitleme.
/// Eğitimde ve tahminde aynı fonksiyon kullanılmalıdır; aksi halde mesafeler anlamsızlaşır.
/// </summary>
public static class FacePreprocessor
{
    public const int FaceSize = 100;

    /// <summary>Görüntüden yüz bölgesini kesip hazırlar.</summary>
    public static Mat Prepare(Mat image, Rect face)
    {
        Rect bounds = ClampToImage(face, image);
        if (bounds.Width == 0 || bounds.Height == 0)
        {
            throw new ArgumentException("Yüz bölgesi görüntünün dışında.", nameof(face));
        }

        using var crop = new Mat(image, bounds);
        return Prepare(crop);
    }

    /// <summary>Kesilmiş bir yüz görüntüsünü hazırlar.</summary>
    public static Mat Prepare(Mat faceImage)
    {
        using var gray = new Mat();
        if (faceImage.Channels() == 1)
        {
            faceImage.CopyTo(gray);
        }
        else
        {
            Cv2.CvtColor(faceImage, gray, faceImage.Channels() == 4 ? ColorConversionCodes.BGRA2GRAY : ColorConversionCodes.BGR2GRAY);
        }

        var result = new Mat();
        InterpolationFlags interpolation = gray.Width > FaceSize ? InterpolationFlags.Area : InterpolationFlags.Linear;
        Cv2.Resize(gray, result, new Size(FaceSize, FaceSize), 0, 0, interpolation);
        Cv2.EqualizeHist(result, result);
        return result;
    }

    public static Rect ClampToImage(Rect rect, Mat image) => rect.Intersect(new Rect(0, 0, image.Width, image.Height));
}
