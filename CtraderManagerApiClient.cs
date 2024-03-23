using System.Buffers;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading.Channels;
using CtraderApi.Exceptions;
using CtraderApi.Helpers;
using Google.Protobuf;

namespace CtraderApi;

public sealed class CtraderManagerApiClient : IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly string _host;
    private readonly Channel<ProtoMessage> _messagesChannel = Channel.CreateUnbounded<ProtoMessage>();
    private readonly int _port;
    private bool _isDisposed;
    private SslStream? _sslStream;
    private TcpClient? _tcpClient;
    private Dictionary<string, TaskCompletionSource<IMessage>> _continuations = [];

    public CtraderManagerApiClient(string host, int port)
    {
        _host = host;
        _port = port;
    }

    public void Dispose()
    {
        if (_isDisposed) throw new InvalidOperationException("Not expected to be disposed second time");
        _isDisposed = true;
        _cancellationTokenSource.Cancel();
        _ = _messagesChannel.Writer.TryComplete();
        _cancellationTokenSource.Dispose();
        _sslStream?.Dispose();
        _tcpClient?.Dispose();
    }

    public event Action<IMessage>? MessageWithoutIdReceived;
    public event Action<Exception>? Error;

    public async Task Connect()
    {
        ThrowObjectDisposedExceptionIfDisposed();

        try
        {
            _tcpClient = new TcpClient { LingerState = new LingerOption(true, 10) };
            await _tcpClient.ConnectAsync(_host, _port);
            _sslStream = new SslStream(_tcpClient.GetStream(), false);
            await _sslStream.AuthenticateAsClientAsync(_host);

            _ = Task.Run(() => ReadTcp(_cancellationTokenSource.Token));
            _ = StartSendingMessages(_cancellationTokenSource.Token);
        }
        catch (Exception ex)
        {
            throw new ConnectionException(ex);
        }
    }

    public async Task<TRes> SendMessage<TReq, TRes>(TReq message) where TReq : IMessage
    {
        var clientMsgId = Guid.NewGuid().ToString("n");
        var taskCompletionSource = new TaskCompletionSource<IMessage>();
        _continuations[clientMsgId] = taskCompletionSource;

        var protoMessage = MessageFactory.GetMessage(message.GetPayloadType(), message.ToByteString(), clientMsgId);

        await _messagesChannel.Writer.WriteAsync(protoMessage);

        return (TRes)await taskCompletionSource.Task;
    }

    private async Task SendMessageInstant(ProtoMessage message)
    {
        ThrowObjectDisposedExceptionIfDisposed();

        var messageByte = message.ToByteArray();

        await WriteTcp(messageByte, _cancellationTokenSource.Token);
    }

    private async Task StartSendingMessages(CancellationToken cancellationToken)
    {
        try
        {
            while (await _messagesChannel.Reader.WaitToReadAsync(cancellationToken) && _isDisposed is false)
                while (_messagesChannel.Reader.TryRead(out var message))
                    if (_isDisposed is false)
                        await SendMessageInstant(message);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            OnError(ex);
        }
    }

    private async Task ReadTcp(CancellationToken cancellationToken)
    {
        var dataLength = new byte[4];
        byte[]? data = null;

        try
        {
            while (!_isDisposed)
            {
                var readBytes = 0;

                do
                {
                    var count = dataLength.Length - readBytes;
                    readBytes += await _sslStream.ThrowIfNull().ReadAsync(dataLength, readBytes, count, cancellationToken)
                        .ConfigureAwait(false);

                    if (readBytes == 0) throw new InvalidOperationException("Remote host closed the connection");
                } while (readBytes < dataLength.Length);

                var length = GetLength(dataLength);
                if (length <= 0) continue;

                data = ArrayPool<byte>.Shared.Rent(length);

                readBytes = 0;

                do
                {
                    var count = length - readBytes;
                    readBytes += await _sslStream.ThrowIfNull().ReadAsync(data, readBytes, count, cancellationToken)
                        .ConfigureAwait(false);
                    if (readBytes == 0) throw new InvalidOperationException("Remote host closed the connection");
                } while (readBytes < length);

                var message = ProtoMessage.Parser.ParseFrom(data, 0, length);

                ArrayPool<byte>.Shared.Return(data);

                OnNext(message);
            }
        }
        catch (Exception ex)
        {
            if (data is not null) ArrayPool<byte>.Shared.Return(data);

            var exception = new ReceiveException(ex);

            OnError(exception);
        }
    }

    private static int GetLength(byte[] lengthBytes)
    {
        var lengthSpan = lengthBytes.AsSpan();

        lengthSpan.Reverse();

        return BitConverter.ToInt32(lengthSpan);
    }

    private async Task WriteTcp(byte[] messageByte, CancellationToken cancellationToken)
    {
        var data = BitConverter.GetBytes(messageByte.Length).Reverse().Concat(messageByte).ToArray();

        var stream = _sslStream.ThrowIfNull();
        await stream.WriteAsync(data, 0, data.Length, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private void OnNext(ProtoMessage protoMessage)
    {
        var message = PayloadParser.ParseMessage(protoMessage);

        if (protoMessage.ClientMsgId != null)
        {
            if (_continuations.Remove(protoMessage.ClientMsgId, out var tcs))
            {
                tcs.SetResult(message);
            }
        }
        else
        {
            MessageWithoutIdReceived?.Invoke(message);
        }
    }

    private void OnError(Exception exception)
    {
        Error?.Invoke(exception);
    }

    private void ThrowObjectDisposedExceptionIfDisposed()
    {
        if (_isDisposed) throw new ObjectDisposedException(GetType().FullName);
    }
}