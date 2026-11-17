using OpenCvSharp;
using Size = OpenCvSharp.Size;

namespace FaceRecognition.Services;

public static class FacePreprocessor
{
    public const int FaceSize = 100;

    public static Rect Expand(Rect face, Mat image, double margin = 0.18)
    {
        int padX = Math.Max(1, (int)(face.Width * margin));
        int padY = Math.Max(1, (int)(face.Height * margin));
        return ClampToImage(new Rect(face.X - padX, face.Y - padY, face.Width + 2 * padX, face.Height + 2 * padY), image);
    }

    public static Mat Prepare(Mat image, Rect face)
    {
        Rect bounds = Expand(face, image, 0.12);
        if (bounds.Width == 0 || bounds.Height == 0)
        {
            throw new ArgumentException("Yüz bölgesi görüntünün dışında.", nameof(face));
        }

        using var crop = new Mat(image, bounds);
        return Prepare(crop);
    }

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
