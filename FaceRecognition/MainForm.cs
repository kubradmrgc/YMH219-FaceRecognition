namespace FaceRecognition;

/// <summary>
/// Kameradan veya dosyadan alınan görüntüde yüz algılayan ve tanıyan ana pencere.
/// </summary>
public partial class MainForm : Form
{
    public MainForm(string openCvVersion)
    {
        BuildLayout();
        versionLabel.Text = $"OpenCV {openCvVersion}";

        foreach (Control control in new Control[]
                 {
                     cameraCombo, findCamerasButton, startCameraButton, stopCameraButton, selectImageButton,
                     recognitionCheckBox, thresholdTrackBar, saveFacesButton, openCapturesButton,
                     labelNameTextBox, addSampleButton, addFiveSamplesButton, deleteLabelButton, openDatasetButton, trainButton
                 })
        {
            control.Enabled = false;
        }

        statusLabel.Text = "Hazır.";
    }
}
