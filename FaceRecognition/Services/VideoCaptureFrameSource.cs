using OpenCvSharp;

namespace FaceRecognition.Services;

public sealed class VideoCaptureFrameSource : IFrameSource
{
    public const int RequestedWidth = 640;
    public const int RequestedHeight = 480;

    // DirectShow çoğu dizüstü kamerasında daha hızlı açılır; açılmazsa Media Foundation denenir.
    private static readonly VideoCaptureAPIs[] Backends = { VideoCaptureAPIs.DSHOW, VideoCaptureAPIs.MSMF };

    private readonly VideoCapture capture;

    private VideoCaptureFrameSource(VideoCapture capture)
    {
        this.capture = capture;
    }

    public static VideoCaptureFrameSource? TryOpen(int index)
    {
        foreach (VideoCaptureAPIs backend in Backends)
        {
            var capture = new VideoCapture();
            bool opened;
            try
            {
                opened = capture.Open(index, backend) && capture.IsOpened();
            }
            catch (OpenCVException)
            {
                opened = false;
            }

            if (opened)
            {
                capture.Set(VideoCaptureProperties.FrameWidth, RequestedWidth);
                capture.Set(VideoCaptureProperties.FrameHeight, RequestedHeight);
                return new VideoCaptureFrameSource(capture);
            }
            capture.Dispose();
        }
        return null;
    }

    public bool Read(Mat frame) => capture.Read(frame) && !frame.Empty();

    public void Dispose()
    {
        capture.Release();
        capture.Dispose();
    }
}
