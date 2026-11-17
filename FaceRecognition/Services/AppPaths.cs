namespace FaceRecognition.Services;

/// <summary>
/// Uygulamanın dosya konumları. Kişisel yüz görüntüleri ve model depo klasörüne değil,
/// kullanıcının yerel uygulama verisi klasörüne (%LOCALAPPDATA%) yazılır.
/// </summary>
public sealed class AppPaths
{
    public const string AppFolderName = "YMH219-FaceRecognition";

    public AppPaths(string dataRoot, string cascadePath)
    {
        DataRoot = dataRoot;
        CascadePath = cascadePath;
    }

    public static AppPaths Default => new(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppFolderName),
        FaceDetector.DefaultCascadePath);

    public string DataRoot { get; }

    public string CascadePath { get; }

    public string DatasetDirectory => Path.Combine(DataRoot, "dataset");

    public string ModelDirectory => Path.Combine(DataRoot, "model");

    public string ModelFile => Path.Combine(ModelDirectory, "lbph-model.yml");

    public string LabelsFile => Path.Combine(ModelDirectory, "labels.json");

    public string CapturesDirectory => Path.Combine(DataRoot, "captures");
}
