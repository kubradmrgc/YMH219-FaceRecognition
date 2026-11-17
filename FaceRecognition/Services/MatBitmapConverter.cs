using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using OpenCvSharp;

namespace FaceRecognition.Services;

/// <summary>
/// OpenCV Mat'ini Windows Forms'ta gösterilebilecek Bitmap'e çevirir.
/// OpenCvSharp4.Extensions paketi .NET 10'a ait System.Drawing.Common gerektirdiği ve .NET 9 WinForms ile
/// çakıştığı için dönüşüm burada yapılır.
/// </summary>
public static class MatBitmapConverter
{
    public static Bitmap ToBitmap(Mat image)
    {
        if (image.Empty())
        {
            throw new ArgumentException("Boş Mat Bitmap'e çevrilemez.", nameof(image));
        }

        Mat? converted = null;
        Mat source = image;
        if (image.Type() != MatType.CV_8UC3)
        {
            converted = new Mat();
            ColorConversionCodes code = image.Channels() switch
            {
                1 => ColorConversionCodes.GRAY2BGR,
                4 => ColorConversionCodes.BGRA2BGR,
                _ => throw new ArgumentException($"Desteklenmeyen Mat türü: {image.Type()}", nameof(image))
            };
            Cv2.CvtColor(image, converted, code);
            source = converted;
        }

        try
        {
            int width = source.Width;
            int height = source.Height;
            var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
            try
            {
                // GDI+ 24 bit biçimi de bellekte B,G,R sırasındadır; satır uzunlukları farklı olabildiği için satır satır kopyalanır.
                int rowBytes = width * 3;
                var row = new byte[rowBytes];
                for (int y = 0; y < height; y++)
                {
                    Marshal.Copy(source.Ptr(y), row, 0, rowBytes);
                    Marshal.Copy(row, 0, data.Scan0 + y * data.Stride, rowBytes);
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
            return bitmap;
        }
        finally
        {
            converted?.Dispose();
        }
    }
}
