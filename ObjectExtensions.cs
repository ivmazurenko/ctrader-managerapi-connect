namespace CtraderApi;

public static class ObjectExtensions
{
    public static T ThrowIfNull<T>(this T? value)
        where T : class
    {
        if (value is null)
            throw new InvalidOperationException("Not expected null value");

        return value;
    }

    public static T CastTo<T>(this object value) => (T)value;
}