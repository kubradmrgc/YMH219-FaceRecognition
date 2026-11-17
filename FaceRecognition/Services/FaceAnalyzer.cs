using FaceRecognition.Models;
using OpenCvSharp;

namespace FaceRecognition.Services;

/// <summary>
/// Bir görüntüdeki yüzleri bulur ve tanıma açıksa her yüzü eğitilmiş modelle etiketler.
/// Hem arayüz hem kamera iş parçacığından çağrılabilir.
/// </summary>
public sealed class FaceAnalyzer
{
    private readonly FaceDetector detector;
    private readonly FaceRecognizerService recognizer;
    private volatile bool recognitionEnabled;

    public FaceAnalyzer(FaceDetector detector, FaceRecognizerService recognizer)
    {
        this.detector = detector;
        this.recognizer = recognizer;
    }

    public bool RecognitionEnabled
    {
        get => recognitionEnabled;
        set => recognitionEnabled = value;
    }

    public IReadOnlyList<FaceDetection> Analyze(Mat image)
    {
        Rect[] faces = detector.Detect(image);
        bool recognize = recognitionEnabled && recognizer.IsTrained;
        var results = new List<FaceDetection>(faces.Length);
        foreach (Rect face in faces)
        {
            RecognitionResult? recognition = null;
            if (recognize)
            {
                using Mat prepared = FacePreprocessor.Prepare(image, face);
                recognition = recognizer.Predict(prepared);
            }
            results.Add(new FaceDetection(face, recognition));
        }
        return results;
    }
}
