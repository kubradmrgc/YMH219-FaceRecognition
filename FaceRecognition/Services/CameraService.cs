using System.Diagnostics;
using FaceRecognition.Models;
using OpenCvSharp;

namespace FaceRecognition.Services;

public sealed class CameraService : IDisposable
{
    public const int MaxCameraIndex = 3;

    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(3);

    private const string PermissionHint =
        "Windows kamera iznini kontrol edin: Ayarlar > Gizlilik ve güvenlik > Kamera > " +
        "'Kamera erişimi' ve 'Masaüstü uygulamalarının kameranıza erişmesine izin ver' açık olmalı.";

    private readonly Func<int, IFrameSource?> openSource;
    private readonly object sync = new();
    private CancellationTokenSource? cts;
    private Task loop = Task.CompletedTask;
    private volatile CameraState state = CameraState.Stopped;

    public CameraService() : this(VideoCaptureFrameSource.TryOpen)
    {
    }

    public CameraService(Func<int, IFrameSource?> openSource)
    {
        this.openSource = openSource;
    }

    public event Action<Mat>? FrameReady;

    public event Action<CameraState>? StateChanged;

    public event Action<string>? Failed;

    public CameraState State => state;

    public TimeSpan FirstFrameTimeout { get; init; } = TimeSpan.FromSeconds(6);

    public TimeSpan LostFrameTimeout { get; init; } = TimeSpan.FromSeconds(2);

    public static IReadOnlyList<CameraInfo> FindCameras()
    {
        var found = new List<CameraInfo>();
        for (int index = 0; index <= MaxCameraIndex; index++)
        {
            using IFrameSource? source = VideoCaptureFrameSource.TryOpen(index);
            if (source is not null)
            {
                found.Add(new CameraInfo(index));
            }
        }
        return found;
    }

    public void Start(int index)
    {
        lock (sync)
        {
            if (state != CameraState.Stopped)
            {
                throw new InvalidOperationException("Kamera zaten çalışıyor.");
            }

            cts?.Dispose();
            cts = new CancellationTokenSource();
            SetState(CameraState.Starting);
            CancellationToken token = cts.Token;
            loop = Task.Run(() => Run(index, token));
        }
    }

    public Task StopAsync()
    {
        lock (sync)
        {
            if (state is CameraState.Starting or CameraState.Running)
            {
                SetState(CameraState.Stopping);
                cts?.Cancel();
            }
            return loop;
        }
    }

    private void Run(int index, CancellationToken token)
    {
        IFrameSource? source = null;
        try
        {
            source = openSource(index);
            if (source is null)
            {
                ReportFailure(
                    $"Kamera {index} açılamadı.\n\n" +
                    "Olası nedenler:\n" +
                    "• Bu numarada bir kamera yok (farklı bir kamera seçin veya 'Kameraları Ara' düğmesine basın).\n" +
                    "• Kamera başka bir uygulama (Teams, Zoom, Kamera uygulaması, tarayıcı) tarafından kullanılıyor; o uygulamayı kapatın.\n" +
                    "• " + PermissionHint, token);
                return;
            }

            using var frame = new Mat();
            var sinceLastFrame = Stopwatch.StartNew();
            bool receivedAny = false;
            while (!token.IsCancellationRequested)
            {
                if (source.Read(frame))
                {
                    if (!receivedAny)
                    {
                        receivedAny = true;
                        if (!token.IsCancellationRequested)
                        {
                            SetState(CameraState.Running);
                        }
                    }
                    sinceLastFrame.Restart();
                    FrameReady?.Invoke(frame);
                    continue;
                }

                if (!receivedAny && sinceLastFrame.Elapsed > FirstFrameTimeout)
                {
                    ReportFailure(
                        $"Kamera {index} açıldı ancak görüntü gelmiyor.\n\n" +
                        "• Kamera başka bir uygulama tarafından kullanılıyor olabilir; o uygulamayı kapatıp tekrar deneyin.\n" +
                        "• Kamera kapağı veya klavyedeki kamera kapatma tuşu kapalı olabilir.\n" +
                        "• " + PermissionHint, token);
                    return;
                }

                if (receivedAny && sinceLastFrame.Elapsed > LostFrameTimeout)
                {
                    ReportFailure($"Kamera {index} bağlantısı kesildi (görüntü gelmiyor). Kamerayı kontrol edip yeniden başlatın.", token);
                    return;
                }

                token.WaitHandle.WaitOne(20);
            }
        }
        catch (Exception ex)
        {
            ReportFailure($"Kamera {index} çalışırken hata oluştu: {ex.Message}", token);
        }
        finally
        {
            source?.Dispose();
            SetState(CameraState.Stopped);
        }
    }

    private void ReportFailure(string message, CancellationToken token)
    {
        if (!token.IsCancellationRequested)
        {
            Failed?.Invoke(message);
        }
    }

    private void SetState(CameraState newState)
    {
        state = newState;
        StateChanged?.Invoke(newState);
    }

    public void Dispose()
    {
        Task running;
        lock (sync)
        {
            cts?.Cancel();
            running = loop;
        }
        if (running.Wait(StopTimeout))
        {
            cts?.Dispose();
        }
    }
}
