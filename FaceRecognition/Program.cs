namespace FaceRecognition;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => ShowFatal("Beklenmeyen bir hata oluştu", e.Exception);

        string version;
        try
        {
            version = OpenCvSharp.Cv2.GetVersionString() ?? "?";
        }
        catch (Exception ex) when (ex is DllNotFoundException or TypeInitializationException or BadImageFormatException)
        {
            ShowFatal("OpenCV yerel kütüphanesi (OpenCvSharpExtern.dll) yüklenemedi. " +
                      "Uygulama yalnızca 64 bit Windows'ta çalışır; projeyi yeniden derleyip tekrar deneyin.", ex);
            return;
        }

        Application.Run(new MainForm(version));
    }

    private static void ShowFatal(string message, Exception ex)
    {
        MessageBox.Show($"{message}\n\nAyrıntı: {ex.Message}", "YMH219 Yüz Tanıma", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
