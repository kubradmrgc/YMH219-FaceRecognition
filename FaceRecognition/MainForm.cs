using FaceRecognition.Models;
using FaceRecognition.Services;
using Mat = OpenCvSharp.Mat;

namespace FaceRecognition;

/// <summary>
/// Kameradan veya dosyadan alınan görüntüde yüz algılayan ve tanıyan ana pencere.
/// </summary>
public partial class MainForm : Form
{
    private readonly FaceDetector? detector;
    private readonly string? detectorError;
    private Mat? currentImage;
    private string? currentImageName;
    private IReadOnlyList<FaceDetection> currentFaces = Array.Empty<FaceDetection>();
    private string resultsSignature = string.Empty;

    public MainForm(string openCvVersion) : this(openCvVersion, FaceDetector.DefaultCascadePath)
    {
    }

    protected MainForm(string openCvVersion, string cascadePath)
    {
        BuildLayout();
        versionLabel.Text = $"OpenCV {openCvVersion}";

        try
        {
            detector = new FaceDetector(cascadePath);
        }
        catch (FaceAppException ex)
        {
            detectorError = ex.Message;
        }

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
        UpdateResults(currentFaces);
        statusLabel.Text = detector is null
            ? "Yüz algılama devre dışı (Haar cascade yüklenemedi). Resimler yine de görüntülenebilir."
            : "Hazır. 'Görüntü Seç...' ile bir resim açabilirsiniz.";
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (detectorError is not null)
        {
            ShowError(detectorError + "\n\nUygulama açık kalacak ancak yüz algılama devre dışı.");
        }
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
        AnalyzeCurrentImage();

        string size = $"{loaded.OriginalSize.Width}x{loaded.OriginalSize.Height}";
        string resized = loaded.WasResized ? $"; işlem için {currentImage.Width}x{currentImage.Height} boyutuna küçültüldü" : "";
        statusLabel.Text = $"{currentImageName} açıldı ({size}{resized}). {DescribeFaceCount(currentFaces.Count)}";
        UpdateActiveOperation();
    }

    private void AnalyzeCurrentImage()
    {
        if (currentImage is null)
        {
            return;
        }

        Cursor = Cursors.WaitCursor;
        try
        {
            currentFaces = DetectFaces(currentImage);
            SetDisplayImage(FaceOverlayRenderer.Render(currentImage, currentFaces));
            UpdateResults(currentFaces);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private IReadOnlyList<FaceDetection> DetectFaces(Mat image)
    {
        if (detector is null)
        {
            return Array.Empty<FaceDetection>();
        }
        return detector.Detect(image).Select(rect => new FaceDetection(rect)).ToArray();
    }

    private string DescribeFaceCount(int count)
    {
        if (detector is null)
        {
            return "Yüz algılama devre dışı.";
        }
        return count == 0 ? "Yüz bulunamadı." : $"{count} yüz algılandı.";
    }

    private void UpdateResults(IReadOnlyList<FaceDetection> faces)
    {
        faceCountLabel.Text = detector is null
            ? "Algılanan yüz: - (algılama devre dışı)"
            : $"Algılanan yüz: {faces.Count}";

        var rows = faces.Select((face, i) => new[] { (i + 1).ToString(), face.Caption(i + 1), "-", "-" }).ToList();
        string signature = string.Join("|", rows.Select(r => string.Join(";", r)));
        if (signature == resultsSignature)
        {
            return;
        }

        resultsSignature = signature;
        resultsList.BeginUpdate();
        resultsList.Items.Clear();
        foreach (string[] row in rows)
        {
            resultsList.Items.Add(new ListViewItem(row));
        }
        resultsList.EndUpdate();
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
        detector?.Dispose();
        base.OnFormClosed(e);
    }
}
