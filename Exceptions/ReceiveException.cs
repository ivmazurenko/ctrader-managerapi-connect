namespace CtraderApi.Exceptions;

public class ReceiveException : Exception
{
    public ReceiveException(Exception innerException) : base("An exception occurred while reading from stream",
        innerException)
    {
    }
}