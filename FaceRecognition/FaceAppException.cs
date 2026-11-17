namespace FaceRecognition;

/// <summary>
/// Kullanıcıya doğrudan gösterilebilecek Türkçe hata mesajı taşıyan uygulama istisnası.
/// </summary>
public class FaceAppException : Exception
{
    public FaceAppException(string message) : base(message)
    {
    }

    public FaceAppException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
