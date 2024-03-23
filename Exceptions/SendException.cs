namespace CtraderApi.Exceptions;

public class SendException : Exception
{
    public SendException(Exception innerException) : base("An exception occurred while writing on stream",
        innerException)
    {
    }
}