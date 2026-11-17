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
    private const int BurstSampleCount = 5;
    private const int BurstMaxAttempts = 20;

    private readonly AppPaths paths;
    private readonly FaceDataset dataset;
    private readonly FaceDetector? detector;
    private readonly string? detectorError;
    private readonly CameraService camera;
    private readonly Stopwatch fpsWatch = new();
    private readonly System.Windows.Forms.Timer burstTimer = new() { Interval = 700 };
    private int fpsFrames;
    private int framePending;
    private int frameSequence;
    private bool searchingCameras;
    private Mat? currentImage;
    private string? currentImageName;
    private IReadOnlyList<FaceDetection> currentFaces = Array.Empty<FaceDetection>();
    private string resultsSignature = string.Empty;
    private string? burstLabel;
    private int burstAdded;
    private int burstAttempts;
    private int burstLastFrame;

    public MainForm(string openCvVersion) : this(openCvVersion, AppPaths.Default, new CameraService())
    {
    }

    protected MainForm(string openCvVersion, AppPaths paths, CameraService camera)
    {
        this.paths = paths;
        this.camera = camera;
        dataset = new FaceDataset(paths.DatasetDirectory);
        BuildLayout();
        versionLabel.Text = $"OpenCV {openCvVersion}";

        try
        {
            detector = new FaceDetector(paths.CascadePath);
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

        addSampleButton.Click += (_, _) => AddSample();
        addFiveSamplesButton.Click += (_, _) => StartBurst();
        burstTimer.Tick += (_, _) => OnBurstTick();
        deleteLabelButton.Click += (_, _) => DeleteSelectedLabel();
        openDatasetButton.Click += (_, _) => OpenFolder(dataset.DirectoryPath);
        labelsList.SelectedIndexChanged += (_, _) =>
        {
            if (labelsList.SelectedItems.Count > 0 && labelsList.SelectedItems[0].Tag is LabelInfo info)
            {
                labelNameTextBox.Text = info.Name;
            }
            UpdateSampleButtons();
        };

        foreach (Control control in new Control[] { recognitionCheckBox, thresholdTrackBar, saveFacesButton, openCapturesButton, trainButton })
        {
            control.Enabled = false;
        }

        UpdateCameraState(CameraState.Stopped);
        UpdateResults(currentFaces);
        RefreshLabels();
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
        frameSequence++;
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
            if (burstLabel is not null)
            {
                StopBurst($"Kamera kapandığı için örnek toplama durdu ({burstAdded}/{BurstSampleCount} örnek eklendi).");
            }
        }
        UpdateSampleButtons();
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
        UpdateSampleButtons();
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

    // ---------------- Veri seti ----------------

    private void AddSample()
    {
        try
        {
            string label = FaceDataset.ValidateLabel(labelNameTextBox.Text);
            string used = AddSampleFromCurrentFrame(label);
            labelNameTextBox.Text = used;
            RefreshLabels();
            statusLabel.Text = $"Örnek '{used}' etiketine eklendi (toplam {dataset.CountSamples(used)} örnek).";
        }
        catch (FaceAppException ex)
        {
            ShowWarning(ex.Message);
        }
    }

    /// <summary>Gösterilen karedeki tek yüzü veri setine ekler ve eklendiği etiketi döndürür.</summary>
    private string AddSampleFromCurrentFrame(string label)
    {
        if (detector is null)
        {
            throw new FaceAppException("Yüz algılama devre dışı olduğu için örnek eklenemiyor (Haar cascade yüklenemedi).");
        }
        if (currentImage is null)
        {
            throw new FaceAppException("Önce kamerayı başlatın veya bir resim açın.");
        }
        if (currentFaces.Count != 1)
        {
            throw new FaceAppException(currentFaces.Count == 0
                ? "Karede yüz bulunamadı. Yüzünüzü kameraya doğru çevirin veya yüz içeren bir resim seçin."
                : $"Karede {currentFaces.Count} yüz var. Örnek eklemek için karede tam olarak bir yüz olmalı.");
        }

        using var crop = new Mat(currentImage, FacePreprocessor.ClampToImage(currentFaces[0].Bounds, currentImage));
        return dataset.AddSample(label, crop);
    }

    private void StartBurst()
    {
        string label;
        try
        {
            label = FaceDataset.ValidateLabel(labelNameTextBox.Text);
        }
        catch (FaceAppException ex)
        {
            ShowWarning(ex.Message);
            return;
        }

        if (camera.State != CameraState.Running)
        {
            ShowWarning($"'{BurstSampleCount} Örnek Çek' yalnızca kamera açıkken kullanılabilir. Önce kamerayı başlatın.");
            return;
        }

        burstLabel = label;
        burstAdded = 0;
        burstAttempts = 0;
        burstLastFrame = -1;
        burstTimer.Start();
        statusLabel.Text = $"'{label}' için örnek toplanıyor: 0/{BurstSampleCount}. Başınızı hafifçe sağa sola çevirin.";
        UpdateSampleButtons();
        UpdateActiveOperation();
    }

    private void OnBurstTick()
    {
        if (burstLabel is null)
        {
            return;
        }

        burstAttempts++;
        if (frameSequence != burstLastFrame && currentFaces.Count == 1)
        {
            try
            {
                burstLabel = AddSampleFromCurrentFrame(burstLabel);
                burstAdded++;
                burstLastFrame = frameSequence;
                RefreshLabels();
            }
            catch (FaceAppException ex)
            {
                StopBurst(null);
                ShowWarning(ex.Message);
                return;
            }
        }

        if (burstAdded >= BurstSampleCount)
        {
            StopBurst($"{BurstSampleCount} örnek '{burstLabel}' etiketine eklendi (toplam {dataset.CountSamples(burstLabel)} örnek).");
        }
        else if (burstAttempts >= BurstMaxAttempts)
        {
            StopBurst($"Örnek toplama durdu: {burstAdded}/{BurstSampleCount} örnek eklenebildi. Karede tam olarak bir yüz olduğundan emin olun.");
        }
        else
        {
            statusLabel.Text = $"'{burstLabel}' için örnek toplanıyor: {burstAdded}/{BurstSampleCount}" +
                               (currentFaces.Count == 1 ? "" : $" (karede {currentFaces.Count} yüz var, bekleniyor)");
            UpdateActiveOperation();
        }
    }

    private void StopBurst(string? message)
    {
        burstTimer.Stop();
        burstLabel = null;
        if (message is not null)
        {
            statusLabel.Text = message;
        }
        UpdateSampleButtons();
        UpdateActiveOperation();
    }

    private void DeleteSelectedLabel()
    {
        if (labelsList.SelectedItems.Count == 0 || labelsList.SelectedItems[0].Tag is not LabelInfo info)
        {
            ShowWarning("Silmek için listeden bir etiket seçin.");
            return;
        }

        if (!Confirm($"'{info.Name}' etiketi ve {info.SampleCount} örnek yüz görüntüsü kalıcı olarak silinecek.\n\nDevam edilsin mi?", "Etiketi Sil"))
        {
            return;
        }

        try
        {
            dataset.DeleteLabel(info.Name);
            labelNameTextBox.Clear();
            statusLabel.Text = $"'{info.Name}' etiketi ve {info.SampleCount} örnek silindi.";
        }
        catch (FaceAppException ex)
        {
            ShowWarning(ex.Message);
        }
        RefreshLabels();
    }

    private void RefreshLabels()
    {
        IReadOnlyList<LabelInfo> labels;
        try
        {
            labels = dataset.GetLabels();
        }
        catch (FaceAppException ex)
        {
            labels = Array.Empty<LabelInfo>();
            statusLabel.Text = ex.Message.Split('\n')[0];
        }

        string? selected = (labelsList.SelectedItems.Count > 0 ? labelsList.SelectedItems[0].Tag as LabelInfo : null)?.Name;
        labelsList.BeginUpdate();
        labelsList.Items.Clear();
        foreach (LabelInfo info in labels)
        {
            var item = new ListViewItem(new[] { info.Name, info.SampleCount.ToString() }) { Tag = info };
            if (info.SampleCount < FaceDataset.RecommendedSamplesPerLabel)
            {
                item.ForeColor = Color.DarkOrange;
            }
            labelsList.Items.Add(item);
            item.Selected = info.Name == selected;
        }
        labelsList.EndUpdate();

        UpdateDatasetInfo(labels);
        UpdateSampleButtons();
    }

    private void UpdateDatasetInfo(IReadOnlyList<LabelInfo> labels)
    {
        int total = labels.Sum(l => l.SampleCount);
        var usable = labels.Where(l => l.SampleCount > 0).ToList();
        var few = usable.Where(l => l.SampleCount < FaceDataset.RecommendedSamplesPerLabel).ToList();
        string summary = $"{labels.Count} etiket, toplam {total} örnek.";

        if (usable.Count < FaceDataset.MinLabelsForTraining)
        {
            datasetInfoLabel.Text = $"{summary} Eğitim için örneği olan en az {FaceDataset.MinLabelsForTraining} etiket gerekir " +
                                    $"(etiket başına en az {FaceDataset.RecommendedSamplesPerLabel} örnek önerilir).";
            datasetInfoLabel.ForeColor = Color.Firebrick;
        }
        else if (few.Count > 0)
        {
            datasetInfoLabel.Text = $"{summary} Eğitilebilir; ancak şu etiketlerde önerilen {FaceDataset.RecommendedSamplesPerLabel} örnekten az var: " +
                                    string.Join(", ", few.Select(l => $"{l.Name} ({l.SampleCount})"));
            datasetInfoLabel.ForeColor = Color.DarkOrange;
        }
        else
        {
            datasetInfoLabel.Text = $"{summary} ✓ Eğitime hazır.";
            datasetInfoLabel.ForeColor = Color.SeaGreen;
        }
    }

    private void UpdateSampleButtons()
    {
        bool bursting = burstLabel is not null;
        addSampleButton.Enabled = detector is not null && currentImage is not null && !bursting;
        addFiveSamplesButton.Enabled = detector is not null && camera.State == CameraState.Running && !bursting;
        deleteLabelButton.Enabled = labelsList.SelectedItems.Count > 0 && !bursting;
        labelNameTextBox.ReadOnly = bursting;
    }

    private void OpenFolder(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            ShowWarning($"Klasör açılamadı:\n{folder}\n{ex.Message}");
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
        if (burstLabel is not null)
        {
            operations.Add($"Örnek toplanıyor ({burstAdded}/{BurstSampleCount})");
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

    protected virtual bool Confirm(string message, string caption)
    {
        return MessageBox.Show(this, message, caption, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
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
        burstTimer.Dispose();
        camera.FrameReady -= OnCameraFrame;
        camera.Dispose();
        SetDisplayImage(null);
        currentImage?.Dispose();
        detector?.Dispose();
        base.OnFormClosed(e);
    }
}
