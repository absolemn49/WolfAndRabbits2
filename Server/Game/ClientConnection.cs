using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace WolfAndRabbits.Server.Game;

public sealed class ClientConnection : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = false
    };

    public string ClientId { get; }
    public string PlayerName { get; set; } = "Người chơi";
    public string? CurrentRoomCode { get; set; }

    private readonly WebSocket _socket;
    private readonly Channel<string> _outbound = Channel.CreateBounded<string>(new BoundedChannelOptions(128)
    {
        FullMode = BoundedChannelFullMode.DropOldest,
        SingleReader = true,
        SingleWriter = false
    });
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _sendLoopTask;

    public bool IsConnected => _socket.State == WebSocketState.Open && !_cts.IsCancellationRequested;

    public ClientConnection(string clientId, WebSocket socket)
    {
        ClientId = clientId;
        _socket = socket;
        _sendLoopTask = Task.Run(SendLoopAsync);
    }

    public void Send<T>(T payload)
    {
        if (!IsConnected) return;
        string json = JsonSerializer.Serialize(payload, JsonOptions);
        _outbound.Writer.TryWrite(json);
    }

    public void SendRawJson(string json)
    {
        if (!IsConnected) return;
        _outbound.Writer.TryWrite(json);
    }

    public static string Serialize<T>(T payload) => JsonSerializer.Serialize(payload, JsonOptions);

    private async Task SendLoopAsync()
    {
        try
        {
            await foreach (var json in _outbound.Reader.ReadAllAsync(_cts.Token))
            {
                if (_socket.State != WebSocketState.Open) break;
                var bytes = Encoding.UTF8.GetBytes(json);
                await _socket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    endOfMessage: true,
                    cancellationToken: _cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (WebSocketException)
        {
        }
        catch (Exception)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        _outbound.Writer.TryComplete();
        _cts.Cancel();
        try
        {
            await _sendLoopTask;
        }
        catch
        {
        }
        _cts.Dispose();
    }
}
