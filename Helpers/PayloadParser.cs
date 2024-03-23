using Google.Protobuf;

namespace CtraderApi.Helpers;

public static class PayloadParser
{
    public static IMessage ParseMessage(ProtoMessage protoMessage)
    {
        var payload = protoMessage.Payload;

        return protoMessage.PayloadType switch
        {
            (int)ProtoPayloadType.HeartbeatEvent => ProtoHeartbeatEvent.Parser.ParseFrom(payload),
            (int)ProtoCSPayloadType.ProtoHelloEvent => ProtoHelloEvent.Parser.ParseFrom(payload),
            (int)ProtoCSPayloadType.ProtoManagerAuthRes => ProtoManagerAuthRes.Parser.ParseFrom(payload),
            (int)ProtoCSPayloadType.ProtoManagerSymbolListRes => ProtoManagerSymbolListRes.Parser.ParseFrom(payload),
            _ => throw new IndexOutOfRangeException($"Event type {protoMessage.PayloadType} is not supported yet")
        };
    }
}