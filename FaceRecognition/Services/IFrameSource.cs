using OpenCvSharp;

namespace FaceRecognition.Services;

public interface IFrameSource : IDisposable
{
    bool Read(Mat frame);
}
