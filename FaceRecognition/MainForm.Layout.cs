namespace FaceRecognition;

/// <summary>
/// Ana pencerenin kontrolleri ve yerleşimi. Olay mantığı MainForm.cs dosyasındadır.
/// </summary>
public partial class MainForm
{
    private const int SidePanelWidth = 430;
    private static readonly Font IndicatorFont = new("Segoe UI", 11f, FontStyle.Bold);
    private static readonly Font BoldFont = new("Segoe UI", 9.5f, FontStyle.Bold);

    // --- Görüntü alanı ---
    private readonly PictureBox imageBox = new()
    {
        Name = "imageBox",
        Dock = DockStyle.Fill,
        SizeMode = PictureBoxSizeMode.Zoom,
        BackColor = Color.FromArgb(32, 32, 36)
    };
    private readonly Label emptyHintLabel = new()
    {
        Name = "emptyHintLabel",
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleCenter,
        ForeColor = Color.Gainsboro,
        BackColor = Color.Transparent,
        Font = new Font("Segoe UI", 12f),
        Text = "Görüntü yok.\nKamerayı başlatın veya 'Görüntü Seç...' ile bir resim açın."
    };

    // --- Sağ panel ---
    private readonly Label stateIndicatorLabel = new()
    {
        Name = "stateIndicatorLabel",
        Dock = DockStyle.Fill,
        Height = 36,
        TextAlign = ContentAlignment.MiddleCenter,
        Font = IndicatorFont,
        ForeColor = Color.White,
        BackColor = Color.DimGray,
        Text = "■ HAZIR",
        Margin = new Padding(3, 3, 3, 6)
    };

    // Kaynak
    private readonly ComboBox cameraCombo = new() { Name = "cameraCombo", DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, Margin = new Padding(3, 6, 3, 3) };
    private readonly Button findCamerasButton = CreateButton("findCamerasButton", "Kameraları Ara");
    private readonly Button startCameraButton = CreateButton("startCameraButton", "▶ Kamerayı Başlat");
    private readonly Button stopCameraButton = CreateButton("stopCameraButton", "■ Kamerayı Durdur");
    private readonly Button selectImageButton = CreateButton("selectImageButton", "Görüntü Seç...");

    // Sonuçlar sekmesi
    private readonly Label faceCountLabel = new() { Name = "faceCountLabel", Text = "Algılanan yüz: -", AutoSize = true, Font = BoldFont, Margin = new Padding(3, 6, 3, 6) };
    private readonly ListView resultsList = new()
    {
        Name = "resultsList",
        Dock = DockStyle.Fill,
        View = View.Details,
        FullRowSelect = true,
        GridLines = true,
        HeaderStyle = ColumnHeaderStyle.Nonclickable
    };
    private readonly CheckBox recognitionCheckBox = new() { Name = "recognitionCheckBox", Text = "Tanıma açık (eğitilmiş model ile etiket tahmini)", AutoSize = true, Margin = new Padding(3, 8, 3, 3) };
    private readonly Label thresholdLabel = new() { Name = "thresholdLabel", Text = "Eşik (en fazla mesafe): 75", AutoSize = true, Margin = new Padding(3, 6, 3, 0) };
    private readonly TrackBar thresholdTrackBar = new()
    {
        Name = "thresholdTrackBar",
        Minimum = 20,
        Maximum = 150,
        Value = 75,
        TickFrequency = 10,
        SmallChange = 1,
        LargeChange = 5,
        Dock = DockStyle.Fill
    };
    private readonly Label thresholdHintLabel = CreateHint("thresholdHintLabel",
        "Mesafe ne kadar küçükse yüz o kadar benzer. Mesafesi eşikten büyük olan yüzler 'Bilinmeyen' gösterilir. " +
        "Güven yüzdesi = 100 − mesafe (sezgisel bir ölçü).");
    private readonly Button saveFacesButton = CreateButton("saveFacesButton", "Algılanan Yüzleri Kaydet");
    private readonly Button openCapturesButton = CreateButton("openCapturesButton", "Kayıt Klasörünü Aç");

    // Veri Ekle / Eğitim sekmesi
    private readonly TextBox labelNameTextBox = new() { Name = "labelNameTextBox", Dock = DockStyle.Fill, MaxLength = 30, PlaceholderText = "ör. Kübra", Margin = new Padding(3, 3, 3, 6) };
    private readonly Button addSampleButton = CreateButton("addSampleButton", "Yüzü Örnek Olarak Ekle");
    private readonly Button addFiveSamplesButton = CreateButton("addFiveSamplesButton", "5 Örnek Çek (aralıklı)");
    private readonly Label sampleHintLabel = CreateHint("sampleHintLabel",
        "Karede tam olarak bir yüz olmalı. '5 Örnek Çek' kamera açıkken yaklaşık 0,7 sn arayla 5 örnek ekler; " +
        "bu sırada başınızı hafifçe çevirin.");
    private readonly ListView labelsList = new()
    {
        Name = "labelsList",
        Dock = DockStyle.Fill,
        View = View.Details,
        FullRowSelect = true,
        GridLines = true,
        MultiSelect = false,
        HideSelection = false,
        HeaderStyle = ColumnHeaderStyle.Nonclickable
    };
    private readonly Button deleteLabelButton = CreateButton("deleteLabelButton", "Etiketi Sil");
    private readonly Button openDatasetButton = CreateButton("openDatasetButton", "Veri Klasörünü Aç");
    private readonly Label datasetInfoLabel = CreateHint("datasetInfoLabel", "Eğitim için en az 2 etiket gerekir; etiket başına en az 5 örnek önerilir.");
    private readonly Button trainButton = CreateButton("trainButton", "Modeli Eğit");

    private readonly TabControl tabs = new() { Name = "tabs", Dock = DockStyle.Fill, Padding = new Point(12, 5) };
    private readonly TabPage resultsTab = new() { Name = "resultsTab", Text = "Sonuçlar", Padding = new Padding(6) };
    private readonly TabPage trainingTab = new() { Name = "trainingTab", Text = "Veri Ekle / Eğitim", Padding = new Padding(6) };

    // --- Durum çubuğu ---
    private readonly StatusStrip statusStrip = new() { Name = "statusStrip" };
    private readonly ToolStripStatusLabel activeOperationLabel = new() { Name = "activeOperationLabel", Text = "Aktif işlem: yok", Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
    private readonly ToolStripStatusLabel modelStatusLabel = new() { Name = "modelStatusLabel", Text = "Model: -" };
    private readonly ToolStripStatusLabel fpsLabel = new() { Name = "fpsLabel", Text = "FPS: -" };
    private readonly ToolStripStatusLabel statusLabel = new() { Name = "statusLabel", Spring = true, TextAlign = ContentAlignment.MiddleLeft, Text = "Hazır" };
    private readonly ToolStripStatusLabel versionLabel = new() { Name = "versionLabel", Text = "OpenCV" };

    private void BuildLayout()
    {
        SuspendLayout();
        Name = "MainForm";
        Text = "YMH219 - Yüz Algılama ve Tanıma (OpenCV)";
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(1280, 780);
        MinimumSize = new Size(1040, 680);
        StartPosition = FormStartPosition.CenterScreen;

        imageBox.Controls.Add(emptyHintLabel);

        var sidePanel = new TableLayoutPanel { Name = "sidePanel", Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(4, 2, 2, 2) };
        sidePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        sidePanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        sidePanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        sidePanel.Controls.Add(stateIndicatorLabel, 0, 0);
        sidePanel.Controls.Add(BuildSourceGroup(), 0, 1);
        sidePanel.Controls.Add(tabs, 0, 2);

        BuildResultsTab();
        BuildTrainingTab();
        tabs.TabPages.Add(resultsTab);
        tabs.TabPages.Add(trainingTab);

        var root = new TableLayoutPanel { Name = "rootPanel", Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, SidePanelWidth));
        root.Controls.Add(imageBox, 0, 0);
        root.Controls.Add(sidePanel, 1, 0);

        statusStrip.Items.AddRange(new ToolStripItem[]
        {
            activeOperationLabel, new ToolStripSeparator(),
            modelStatusLabel, new ToolStripSeparator(),
            fpsLabel, new ToolStripSeparator(),
            statusLabel, versionLabel
        });

        Controls.Add(root);
        Controls.Add(statusStrip);
        ResumeLayout(true);
    }

    private GroupBox BuildSourceGroup()
    {
        var table = CreateTable("sourceTable", 2);
        var cameraLabel = new Label { Name = "cameraTitleLabel", Text = "Kamera:", AutoSize = true, Margin = new Padding(3, 10, 3, 3) };
        table.Controls.Add(cameraLabel, 0, 0);
        table.SetColumnSpan(cameraLabel, 2);
        table.Controls.Add(cameraCombo, 0, 1);
        table.Controls.Add(findCamerasButton, 1, 1);
        table.Controls.Add(startCameraButton, 0, 2);
        table.Controls.Add(stopCameraButton, 1, 2);
        table.Controls.Add(selectImageButton, 0, 3);
        table.SetColumnSpan(selectImageButton, 2);

        var group = new GroupBox { Name = "sourceGroup", Text = "Kaynak", Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(6, 2, 6, 6) };
        group.Controls.Add(table);
        return group;
    }

    private void BuildResultsTab()
    {
        resultsList.Columns.Add("#", 36);
        resultsList.Columns.Add("Etiket", 150);
        resultsList.Columns.Add("Mesafe", 75, HorizontalAlignment.Right);
        resultsList.Columns.Add("Güven", 75, HorizontalAlignment.Right);

        var table = CreateTable("resultsTable", 2);
        table.Dock = DockStyle.Fill;
        table.AutoSize = false;
        AddFullRow(table, faceCountLabel);
        int listRow = AddFullRow(table, resultsList);
        AddFullRow(table, recognitionCheckBox);
        AddFullRow(table, thresholdLabel);
        AddFullRow(table, thresholdTrackBar);
        AddFullRow(table, thresholdHintLabel);
        int buttonRow = table.RowCount++;
        table.Controls.Add(saveFacesButton, 0, buttonRow);
        table.Controls.Add(openCapturesButton, 1, buttonRow);
        SetRowStyles(table, listRow);
        resultsTab.Controls.Add(table);
    }

    private void BuildTrainingTab()
    {
        labelsList.Columns.Add("Etiket", 230);
        labelsList.Columns.Add("Örnek sayısı", 110, HorizontalAlignment.Right);

        var table = CreateTable("trainingTable", 2);
        table.Dock = DockStyle.Fill;
        table.AutoSize = false;
        AddFullRow(table, new Label { Name = "labelNameTitleLabel", Text = "Etiket adı (kişi adı):", AutoSize = true, Margin = new Padding(3, 6, 3, 3) });
        AddFullRow(table, labelNameTextBox);
        int addRow = table.RowCount++;
        table.Controls.Add(addSampleButton, 0, addRow);
        table.Controls.Add(addFiveSamplesButton, 1, addRow);
        AddFullRow(table, sampleHintLabel);
        AddFullRow(table, new Label { Name = "labelsTitleLabel", Text = "Etiketler:", AutoSize = true, Font = BoldFont, Margin = new Padding(3, 8, 3, 3) });
        int listRow = AddFullRow(table, labelsList);
        int manageRow = table.RowCount++;
        table.Controls.Add(deleteLabelButton, 0, manageRow);
        table.Controls.Add(openDatasetButton, 1, manageRow);
        AddFullRow(table, datasetInfoLabel);
        AddFullRow(table, trainButton);
        trainButton.Font = BoldFont;
        trainButton.MinimumSize = new Size(0, 38);
        SetRowStyles(table, listRow);
        trainingTab.Controls.Add(table);
    }

    private static TableLayoutPanel CreateTable(string name, int columns)
    {
        var table = new TableLayoutPanel { Name = name, ColumnCount = columns, Dock = DockStyle.Top, AutoSize = true };
        for (int i = 0; i < columns; i++)
        {
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columns));
        }
        return table;
    }

    private static int AddFullRow(TableLayoutPanel table, Control control)
    {
        int row = table.RowCount++;
        table.Controls.Add(control, 0, row);
        table.SetColumnSpan(control, table.ColumnCount);
        return row;
    }

    /// <summary>Listeyi içeren satır kalan alanı doldurur, diğer satırlar içerik kadar yer kaplar.</summary>
    private static void SetRowStyles(TableLayoutPanel table, int fillRow)
    {
        table.RowStyles.Clear();
        for (int i = 0; i < table.RowCount; i++)
        {
            table.RowStyles.Add(i == fillRow ? new RowStyle(SizeType.Percent, 100) : new RowStyle(SizeType.AutoSize));
        }
    }

    private static Label CreateHint(string name, string text)
    {
        return new Label
        {
            Name = name,
            Text = text,
            AutoSize = true,
            MaximumSize = new Size(SidePanelWidth - 40, 0),
            ForeColor = Color.DimGray,
            Margin = new Padding(3, 2, 3, 6)
        };
    }

    private static Button CreateButton(string name, string text)
    {
        return new Button { Name = name, Text = text, Dock = DockStyle.Fill, MinimumSize = new Size(0, 32), Margin = new Padding(3) };
    }
}
