using Google.Protobuf;

namespace CtraderApi.Helpers;

public static class MessageFactory
{
    public static ProtoMessage GetMessage(uint payloadType, ByteString payload, string? clientMessageId = null)
    {
        var message = new ProtoMessage
        {
            PayloadType = payloadType,
            Payload = payload
        };

        if (!string.IsNullOrEmpty(clientMessageId)) message.ClientMsgId = clientMessageId;

        return message;
    }
}