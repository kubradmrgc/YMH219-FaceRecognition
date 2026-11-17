namespace FaceRecognition;

public class FaceAppException : Exception
{
    public FaceAppException(string message) : base(message)
    {
    }

    public FaceAppException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
