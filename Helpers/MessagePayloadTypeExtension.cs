using System.Reflection;
using Google.Protobuf;

namespace CtraderApi.Helpers;

public static class MessagePayloadTypeExtension
{
    public static uint GetPayloadType<T>(this T message) where T : IMessage
    {
        PropertyInfo? property;

        try
        {
            property = message.GetType().GetProperty("PayloadType");
        }
        catch (Exception ex) when (ex is AmbiguousMatchException || ex is ArgumentNullException)
        {
            throw new InvalidOperationException($"Couldn't get the PayloadType of the message {message}", ex);
        }

        return Convert.ToUInt32(property.ThrowIfNull().GetValue(message));
    }
}