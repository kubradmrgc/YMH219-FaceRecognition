using System.Globalization;
using FaceRecognition.Models;
using OpenCvSharp;

namespace FaceRecognition.Services;

public sealed class FaceDataset
{
    public const int MinLabelLength = 2;
    public const int MaxLabelLength = 30;
    public const int MinLabelsForTraining = 2;
    public const int RecommendedSamplesPerLabel = 5;

    public const string UnknownLabel = "Bilinmeyen";

    private const string SamplePattern = "*.png";

    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    private static readonly string[] ReservedWindowsNames =
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    public FaceDataset(string directory)
    {
        DirectoryPath = directory;
    }

    public string DirectoryPath { get; }

    public static string ValidateLabel(string? name)
    {
        string label = (name ?? string.Empty).Trim();
        if (label.Length == 0)
        {
            throw new FaceAppException("Etiket adı boş olamaz. Örnek eklemeden önce kişinin adını yazın.");
        }
        if (label.Length < MinLabelLength || label.Length > MaxLabelLength)
        {
            throw new FaceAppException($"Etiket adı {MinLabelLength}–{MaxLabelLength} karakter olmalı (şu an {label.Length} karakter).");
        }

        char[] invalid = label.Where(c => Path.GetInvalidFileNameChars().Contains(c)).Distinct().ToArray();
        if (invalid.Length > 0)
        {
            string shown = string.Join(" ", invalid.Select(c => char.IsControl(c) ? "(kontrol karakteri)" : c.ToString()));
            throw new FaceAppException($"Etiket adı şu karakterleri içeremez: {shown}\nKullanılamayan karakterler: \\ / : * ? \" < > |");
        }
        if (label.EndsWith('.'))
        {
            throw new FaceAppException("Etiket adı nokta ile bitemez.");
        }
        if (ReservedWindowsNames.Contains(label.ToUpperInvariant()))
        {
            throw new FaceAppException($"'{label}' Windows'ta ayrılmış bir addır; başka bir etiket adı seçin.");
        }
        if (SameLabel(label, UnknownLabel))
        {
            throw new FaceAppException($"'{UnknownLabel}' adı tanınmayan yüzler için ayrılmıştır; başka bir etiket adı seçin.");
        }
        return label;
    }

    public static bool SameLabel(string a, string b) =>
        string.Compare(a, b, Turkish, CompareOptions.IgnoreCase) == 0 ||
        string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<LabelInfo> GetLabels()
    {
        if (!Directory.Exists(DirectoryPath))
        {
            return Array.Empty<LabelInfo>();
        }

        try
        {
            return Directory.GetDirectories(DirectoryPath)
                .Select(dir => new LabelInfo(Path.GetFileName(dir), Directory.GetFiles(dir, SamplePattern).Length))
                .OrderBy(info => info.Name, StringComparer.Create(Turkish, true))
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new FaceAppException($"Veri seti klasörü okunamadı.\n{DirectoryPath}\n{ex.Message}", ex);
        }
    }

    public string? FindLabel(string name) =>
        GetLabels().Select(info => info.Name).FirstOrDefault(existing => SameLabel(existing, name.Trim()));

    public int CountSamples(string label) => GetSampleFiles(label).Count;

    public IReadOnlyList<string> GetSampleFiles(string label)
    {
        string? existing = FindLabel(label);
        if (existing is null)
        {
            return Array.Empty<string>();
        }
        return Directory.GetFiles(Path.Combine(DirectoryPath, existing), SamplePattern).OrderBy(f => f, StringComparer.Ordinal).ToList();
    }

    public string AddSample(string label, Mat faceImage)
    {
        string validated = ValidateLabel(label);
        string target = FindLabel(validated) ?? validated;
        string folder = Path.Combine(DirectoryPath, target);
        string baseName = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
        string path = Path.Combine(folder, baseName + ".png");
        for (int i = 1; File.Exists(path); i++)
        {
            path = Path.Combine(folder, $"{baseName}-{i}.png");
        }

        ImageFileService.WritePng(path, faceImage);
        return target;
    }

    public void DeleteLabel(string label)
    {
        string existing = FindLabel(label) ?? throw new FaceAppException($"'{label}' adlı etiket bulunamadı.");
        try
        {
            Directory.Delete(Path.Combine(DirectoryPath, existing), recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new FaceAppException($"'{existing}' etiketi silinemedi: {ex.Message}", ex);
        }
    }
}
