using OpenCvSharp;

namespace FaceRecognition.Services;

public interface IFaceRecognizer : IDisposable
{
    void Train(IReadOnlyList<Mat> faces, IReadOnlyList<int> labels);

    (int Label, double Distance) Predict(Mat face);

    IReadOnlySet<int> GetTrainedLabels();

    string Serialize();

    void Deserialize(string content);
}
