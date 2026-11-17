using System.Diagnostics;
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
    private readonly CameraService camera = new();
    private readonly Stopwatch fpsWatch = new();
    private int fpsFrames;
    private int framePending;
    private bool searchingCameras;
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

        cameraCombo.Items.Add(new CameraInfo(0));
        cameraCombo.SelectedIndex = 0;

        selectImageButton.Click += async (_, _) => await SelectImageAsync();
        findCamerasButton.Click += async (_, _) => await FindCamerasAsync();
        startCameraButton.Click += (_, _) => StartCamera();
        stopCameraButton.Click += async (_, _) => await StopCameraAsync();

        camera.FrameReady += OnCameraFrame;
        camera.StateChanged += state => RunOnUi(() => UpdateCameraState(state));
        camera.Failed += message => RunOnUi(() => ShowError(message));

        foreach (Control control in new Control[]
                 {
                     recognitionCheckBox, thresholdTrackBar, saveFacesButton, openCapturesButton,
                     labelNameTextBox, addSampleButton, addFiveSamplesButton, deleteLabelButton, openDatasetButton, trainButton
                 })
        {
            control.Enabled = false;
        }

        UpdateCameraState(CameraState.Stopped);
        UpdateResults(currentFaces);
        statusLabel.Text = detector is null
            ? "Yüz algılama devre dışı (Haar cascade yüklenemedi). Görüntüler yine de gösterilebilir."
            : "Hazır. Kamerayı başlatın veya 'Görüntü Seç...' ile bir resim açın.";
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (detectorError is not null)
        {
            ShowError(detectorError + "\n\nUygulama açık kalacak ancak yüz algılama devre dışı.");
        }
    }

    // ---------------- Kamera ----------------

    private async Task FindCamerasAsync()
    {
        searchingCameras = true;
        UpdateCameraState(camera.State);
        statusLabel.Text = $"Kameralar aranıyor (0–{CameraService.MaxCameraIndex} numaraları deneniyor)...";

        IReadOnlyList<CameraInfo> cameras;
        try
        {
            cameras = await Task.Run(CameraService.FindCameras);
        }
        catch (Exception ex)
        {
            cameras = Array.Empty<CameraInfo>();
            statusLabel.Text = $"Kameralar aranırken hata oluştu: {ex.Message}";
        }
        finally
        {
            searchingCameras = false;
        }

        if (IsDisposed)
        {
            return;
        }

        cameraCombo.Items.Clear();
        if (cameras.Count == 0)
        {
            cameraCombo.Items.Add(new CameraInfo(0));
            cameraCombo.SelectedIndex = 0;
            UpdateCameraState(camera.State);
            ShowWarning($"Kullanılabilir kamera bulunamadı (0–{CameraService.MaxCameraIndex} numaralı kameralar denendi).\n\n" +
                        "Kameranın bağlı olduğundan, başka bir uygulamanın kullanmadığından ve Windows kamera izninin açık olduğundan emin olun " +
                        "(Ayarlar > Gizlilik ve güvenlik > Kamera).");
            return;
        }

        foreach (CameraInfo info in cameras)
        {
            cameraCombo.Items.Add(info);
        }
        cameraCombo.SelectedIndex = 0;
        UpdateCameraState(camera.State);
        statusLabel.Text = $"{cameras.Count} kamera bulundu: {string.Join(", ", cameras)}";
    }

    private void StartCamera()
    {
        if (cameraCombo.SelectedItem is not CameraInfo info)
        {
            ShowWarning("Önce listeden bir kamera seçin.");
            return;
        }

        try
        {
            camera.Start(info.Index);
            statusLabel.Text = $"{info} açılıyor...";
        }
        catch (InvalidOperationException ex)
        {
            ShowWarning(ex.Message);
        }
    }

    private async Task StopCameraAsync()
    {
        if (camera.State == CameraState.Stopped)
        {
            return;
        }

        statusLabel.Text = "Kamera durduruluyor...";
        await camera.StopAsync();
        statusLabel.Text = "Kamera durduruldu. Son kare ekranda kaldı.";
    }

    /// <summary>Kamera iş parçacığında çalışır: algılama ve çizim burada yapılır, arayüze yalnızca sonuç gönderilir.</summary>
    private void OnCameraFrame(Mat frame)
    {
        // Arayüz önceki kareyi henüz göstermediyse bu kare atlanır; böylece bekleyen kareler birikip belleği doldurmaz.
        if (Interlocked.CompareExchange(ref framePending, 1, 0) != 0)
        {
            return;
        }

        Mat copy = frame.Clone();
        Bitmap? bitmap = null;
        try
        {
            IReadOnlyList<FaceDetection> faces = DetectFaces(copy);
            bitmap = FaceOverlayRenderer.Render(copy, faces);
            Bitmap shown = bitmap;
            if (!RunOnUi(() => ShowCameraFrame(copy, faces, shown)))
            {
                copy.Dispose();
                bitmap.Dispose();
                Interlocked.Exchange(ref framePending, 0);
            }
        }
        catch
        {
            copy.Dispose();
            bitmap?.Dispose();
            Interlocked.Exchange(ref framePending, 0);
            throw;
        }
    }

    private void ShowCameraFrame(Mat frame, IReadOnlyList<FaceDetection> faces, Bitmap bitmap)
    {
        Interlocked.Exchange(ref framePending, 0);
        if (camera.State != CameraState.Running || IsDisposed)
        {
            frame.Dispose();
            bitmap.Dispose();
            return;
        }

        currentImage?.Dispose();
        currentImage = frame;
        currentImageName = null;
        currentFaces = faces;
        SetDisplayImage(bitmap);
        UpdateResults(faces);
        CountFrame();
    }

    private void CountFrame()
    {
        fpsFrames++;
        if (!fpsWatch.IsRunning)
        {
            fpsWatch.Start();
        }
        else if (fpsWatch.ElapsedMilliseconds >= 1000)
        {
            fpsLabel.Text = $"FPS: {fpsFrames * 1000.0 / fpsWatch.ElapsedMilliseconds:0.0}";
            fpsFrames = 0;
            fpsWatch.Restart();
        }
    }

    private void UpdateCameraState(CameraState state)
    {
        bool stopped = state == CameraState.Stopped;
        startCameraButton.Enabled = stopped && !searchingCameras;
        stopCameraButton.Enabled = state is CameraState.Starting or CameraState.Running;
        findCamerasButton.Enabled = stopped && !searchingCameras;
        cameraCombo.Enabled = stopped && !searchingCameras;

        (stateIndicatorLabel.Text, stateIndicatorLabel.BackColor) = state switch
        {
            CameraState.Starting => ("● KAMERA AÇILIYOR", Color.Goldenrod),
            CameraState.Running => ("● KAMERA AÇIK", Color.SeaGreen),
            CameraState.Stopping => ("● KAMERA KAPANIYOR", Color.Goldenrod),
            _ => ("■ HAZIR", Color.DimGray)
        };

        if (state == CameraState.Running)
        {
            statusLabel.Text = $"{cameraCombo.SelectedItem} açık. Yüzünüzü kameraya doğru çevirin.";
        }
        if (state != CameraState.Running)
        {
            fpsWatch.Reset();
            fpsFrames = 0;
            fpsLabel.Text = "FPS: -";
        }
        UpdateActiveOperation();
    }

    // ---------------- Resim ----------------

    private async Task SelectImageAsync()
    {
        if (camera.State != CameraState.Stopped)
        {
            await camera.StopAsync();
            statusLabel.Text = "Resim seçmek için kamera durduruldu.";
        }

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
        if (camera.State != CameraState.Stopped)
        {
            ShowWarning("Resim açmadan önce kamerayı durdurun.");
            return;
        }

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

    // ---------------- Algılama ve sonuçlar ----------------

    /// <summary>Hem arayüz hem kamera iş parçacığından çağrılır.</summary>
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
        faceCountLabel.Text = detector is null ? "Algılanan yüz: - (algılama devre dışı)"
            : currentImage is null ? "Algılanan yüz: -"
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

    // ---------------- Yardımcılar ----------------

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
        var operations = new List<string>();
        switch (camera.State)
        {
            case CameraState.Starting:
                operations.Add("Kamera açılıyor");
                break;
            case CameraState.Running:
                operations.Add("Kamera açık");
                break;
            case CameraState.Stopping:
                operations.Add("Kamera kapanıyor");
                break;
        }

        if (searchingCameras)
        {
            operations.Add("Kameralar aranıyor");
        }
        if (operations.Count == 0 && currentImage is not null)
        {
            operations.Add(currentImageName is null ? "Son kamera karesi gösteriliyor" : "Resim gösteriliyor");
        }

        activeOperationLabel.Text = operations.Count == 0
            ? "Aktif işlem: yok"
            : "Aktif işlem: " + string.Join(" + ", operations);
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

    /// <summary>Arka plan iş parçacığından arayüze iş gönderir; pencere kapanıyorsa göndermez ve false döner.</summary>
    private bool RunOnUi(Action action)
    {
        if (IsDisposed || Disposing || !IsHandleCreated)
        {
            return false;
        }

        try
        {
            BeginInvoke(action);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        camera.FrameReady -= OnCameraFrame;
        camera.Dispose();
        SetDisplayImage(null);
        currentImage?.Dispose();
        detector?.Dispose();
        base.OnFormClosed(e);
    }
}
