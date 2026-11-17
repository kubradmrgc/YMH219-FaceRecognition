using FaceRecognition.Models;
using FaceRecognition.Services;
using Mat = OpenCvSharp.Mat;

namespace FaceRecognition;

/// <summary>
/// Kameradan veya dosyadan alınan görüntüde yüz algılayan ve tanıyan ana pencere.
/// </summary>
public partial class MainForm : Form
{
    private Mat? currentImage;
    private string? currentImageName;

    public MainForm(string openCvVersion)
    {
        BuildLayout();
        versionLabel.Text = $"OpenCV {openCvVersion}";

        selectImageButton.Click += (_, _) => SelectImage();

        foreach (Control control in new Control[]
                 {
                     cameraCombo, findCamerasButton, startCameraButton, stopCameraButton,
                     recognitionCheckBox, thresholdTrackBar, saveFacesButton, openCapturesButton,
                     labelNameTextBox, addSampleButton, addFiveSamplesButton, deleteLabelButton, openDatasetButton, trainButton
                 })
        {
            control.Enabled = false;
        }

        UpdateActiveOperation();
        statusLabel.Text = "Hazır. 'Görüntü Seç...' ile bir resim açabilirsiniz.";
    }

    private void SelectImage()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Yüz algılanacak resmi seçin",
            Filter = ImageFileService.OpenFileFilter,
            RestoreDirectory = true
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            LoadImage(dialog.FileName);
        }
    }

    private void LoadImage(string path)
    {
        LoadedImage loaded;
        try
        {
            loaded = ImageFileService.Load(path);
        }
        catch (FaceAppException ex)
        {
            ShowWarning(ex.Message);
            return;
        }

        currentImage?.Dispose();
        currentImage = loaded.Image;
        currentImageName = Path.GetFileName(path);
        SetDisplayImage(MatBitmapConverter.ToBitmap(currentImage));

        string size = $"{loaded.OriginalSize.Width}x{loaded.OriginalSize.Height}";
        statusLabel.Text = loaded.WasResized
            ? $"{currentImageName} açıldı ({size}; işlem için {currentImage.Width}x{currentImage.Height} boyutuna küçültüldü)."
            : $"{currentImageName} açıldı ({size}).";
        UpdateActiveOperation();
    }

    /// <summary>Gösterilen resmi değiştirir; önceki Bitmap bellekten atılır.</summary>
    private void SetDisplayImage(Bitmap? bitmap)
    {
        Image? old = imageBox.Image;
        imageBox.Image = bitmap;
        old?.Dispose();
        emptyHintLabel.Visible = bitmap is null;
    }

    private void UpdateActiveOperation()
    {
        activeOperationLabel.Text = currentImage is null
            ? "Aktif işlem: yok"
            : "Aktif işlem: Resim gösteriliyor";
    }

    private void ShowWarning(string message)
    {
        statusLabel.Text = message.Split('\n')[0];
        ShowMessage(message, "Uyarı", MessageBoxIcon.Warning);
    }

    private void ShowError(string message)
    {
        statusLabel.Text = message.Split('\n')[0];
        ShowMessage(message, "Hata", MessageBoxIcon.Error);
    }

    protected virtual void ShowMessage(string message, string caption, MessageBoxIcon icon)
    {
        MessageBox.Show(this, message, caption, MessageBoxButtons.OK, icon);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        SetDisplayImage(null);
        currentImage?.Dispose();
        base.OnFormClosed(e);
    }
}
