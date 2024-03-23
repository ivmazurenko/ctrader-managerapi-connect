namespace CtraderApi.Exceptions;

public class ConnectionException : Exception
{
    public ConnectionException(Exception innerException) : base(
        "An exception occurred during OpenClient connection attempt", innerException)
    {
    }
}