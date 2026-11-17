using System.Drawing.Drawing2D;
using System.Drawing.Text;
using FaceRecognition.Models;
using OpenCvSharp;

namespace FaceRecognition.Services;

/// <summary>
/// Görüntüyü Bitmap'e çevirip her yüzün etrafına dikdörtgen ve etiket çizer.
/// Yazılar GDI+ ile çizilir çünkü Cv2.PutText Türkçe karakterleri (ğ, ş, ı, İ) gösteremez.
/// </summary>
public static class FaceOverlayRenderer
{
    private static readonly Color DetectedColor = Color.Gold;
    private static readonly Color KnownColor = Color.LimeGreen;
    private static readonly Color UnknownColor = Color.OrangeRed;

    public static Bitmap Render(Mat image, IReadOnlyList<FaceDetection> faces)
    {
        Bitmap bitmap = MatBitmapConverter.ToBitmap(image);
        if (faces.Count == 0)
        {
            return bitmap;
        }

        int shortSide = Math.Min(bitmap.Width, bitmap.Height);
        float penWidth = Math.Max(2f, shortSide / 180f);
        float fontSize = Math.Max(13f, shortSide / 22f);

        using Graphics g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        using var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);

        for (int i = 0; i < faces.Count; i++)
        {
            FaceDetection face = faces[i];
            Color color = face.Recognition is null ? DetectedColor
                : face.Recognition.IsKnown ? KnownColor
                : UnknownColor;
            var box = new Rectangle(face.Bounds.X, face.Bounds.Y, face.Bounds.Width, face.Bounds.Height);
            using (var pen = new Pen(color, penWidth))
            {
                g.DrawRectangle(pen, box);
            }

            string caption = face.Caption(i + 1);
            SizeF textSize = g.MeasureString(caption, font);
            float labelHeight = textSize.Height + 2;
            float labelTop = box.Top - labelHeight >= 0 ? box.Top - labelHeight : box.Bottom;
            var labelBox = new RectangleF(box.Left - penWidth / 2, labelTop, textSize.Width + 6, labelHeight);
            using (var background = new SolidBrush(color))
            {
                g.FillRectangle(background, labelBox);
            }
            Brush text = color == UnknownColor ? Brushes.White : Brushes.Black;
            g.DrawString(caption, font, text, labelBox.Left + 3, labelBox.Top + 1);
        }
        return bitmap;
    }
}
