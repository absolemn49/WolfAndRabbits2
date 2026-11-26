using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using WolfAndRabbits.Client.Core;

namespace WolfAndRabbits.Client.Network
{
    public class GameWebSocketClient : MonoBehaviour
    {
        public event Action OnConnected;
        public event Action<string> OnDisconnected;
        public event Action<string> OnError;
        public event Action<string> OnMessageReceived;

        public bool IsConnected { get; private set; }
        public bool IsConnecting { get; private set; }
        public string CurrentUrl { get; private set; }

        private readonly ConcurrentQueue<Action> _mainThreadQueue = new ConcurrentQueue<Action>();

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern int WR_WebSocket_Connect(string url, string gameObjectName);

        [DllImport("__Internal")]
        private static extern void WR_WebSocket_Send(int id, string message);

        [DllImport("__Internal")]
        private static extern void WR_WebSocket_Close(int id);

        private int _webGlSocketId = -1;
#else
        private ClientWebSocket _socket;
        private CancellationTokenSource _cts;
        private readonly ConcurrentQueue<string> _sendQueue = new ConcurrentQueue<string>();
        private SemaphoreSlim _sendSignal;
#endif

        public static string GetDefaultServerUrl()
        {
            return BuildEnvironment.GetDefaultServerUrl();
        }

        public void Connect(string url)
        {
            Disconnect();

            CurrentUrl = string.IsNullOrWhiteSpace(url) ? GetDefaultServerUrl() : url.Trim();
            BuildEnvironment.SaveServerUrl(CurrentUrl);
            IsConnecting = true;
            IsConnected = false;

#if UNITY_WEBGL && !UNITY_EDITOR
            _webGlSocketId = WR_WebSocket_Connect(CurrentUrl, gameObject.name);
            if (_webGlSocketId < 0)
            {
                IsConnecting = false;
                OnError?.Invoke("Không thể khởi tạo WebSocket trình duyệt.");
            }
#else
            _cts = new CancellationTokenSource();
            _sendSignal = new SemaphoreSlim(0);
            while (_sendQueue.TryDequeue(out _)) { }
            Task.Run(() => NativeConnectLoopAsync(CurrentUrl, _cts.Token));
#endif
        }

        public void SendJson(string json)
        {
            if (!IsConnected || string.IsNullOrEmpty(json)) return;

#if UNITY_WEBGL && !UNITY_EDITOR
            if (_webGlSocketId >= 0)
                WR_WebSocket_Send(_webGlSocketId, json);
#else
            _sendQueue.Enqueue(json);
            _sendSignal?.Release();
#endif
        }

        public void SendObject<T>(T payload)
        {
            SendJson(JsonUtility.ToJson(payload));
        }

        public void Disconnect()
        {
            IsConnecting = false;
            IsConnected = false;

#if UNITY_WEBGL && !UNITY_EDITOR
            if (_webGlSocketId >= 0)
            {
                int id = _webGlSocketId;
                _webGlSocketId = -1;
                WR_WebSocket_Close(id);
            }
#else
            if (_cts != null)
            {
                try { _cts.Cancel(); } catch { }
                _cts.Dispose();
                _cts = null;
            }
            if (_socket != null)
            {
                try { _socket.Abort(); } catch { }
                try { _socket.Dispose(); } catch { }
                _socket = null;
            }
            if (_sendSignal != null)
            {
                try { _sendSignal.Dispose(); } catch { }
                _sendSignal = null;
            }
#endif
        }

        private void Update()
        {
            int processed = 0;
            while (processed < 64 && _mainThreadQueue.TryDequeue(out Action act))
            {
                processed++;
                try
                {
                    act?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }

        private void OnDestroy()
        {
            Disconnect();
        }

        private void OnApplicationQuit()
        {
            Disconnect();
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        // Invoked by WebSocketBridge.jslib via SendMessage
        public void OnWebGLOpen(string idStr)
        {
            if (!int.TryParse(idStr, out int id) || id != _webGlSocketId) return;
            IsConnecting = false;
            IsConnected = true;
            OnConnected?.Invoke();
        }

        public void OnWebGLMessage(string payload)
        {
            int sep = payload.IndexOf('|');
            if (sep <= 0) return;
            if (!int.TryParse(payload.Substring(0, sep), out int id) || id != _webGlSocketId) return;
            string json = payload.Substring(sep + 1);
            OnMessageReceived?.Invoke(json);
        }

        public void OnWebGLError(string payload)
        {
            int sep = payload.IndexOf('|');
            string err = sep > 0 ? payload.Substring(sep + 1) : payload;
            IsConnecting = false;
            OnError?.Invoke(err);
        }

        public void OnWebGLClose(string idStr)
        {
            if (!int.TryParse(idStr, out int id) || id != _webGlSocketId) return;
            _webGlSocketId = -1;
            IsConnecting = false;
            IsConnected = false;
            OnDisconnected?.Invoke("Mất kết nối máy chủ.");
        }
#else
        private async Task NativeConnectLoopAsync(string url, CancellationToken ct)
        {
            ClientWebSocket ws = new ClientWebSocket();
            ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(15);
            _socket = ws;

            try
            {
                await ws.ConnectAsync(new Uri(url), ct);
                _mainThreadQueue.Enqueue(() =>
                {
                    IsConnecting = false;
                    IsConnected = true;
                    OnConnected?.Invoke();
                });

                Task sendTask = Task.Run(() => NativeSendLoopAsync(ws, ct), ct);
                Task recvTask = Task.Run(() => NativeReceiveLoopAsync(ws, ct), ct);
                await Task.WhenAny(sendTask, recvTask);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                string msg = ex.Message;
                _mainThreadQueue.Enqueue(() =>
                {
                    IsConnecting = false;
                    IsConnected = false;
                    OnError?.Invoke(msg);
                });
                return;
            }

            if (!ct.IsCancellationRequested)
            {
                _mainThreadQueue.Enqueue(() =>
                {
                    IsConnecting = false;
                    IsConnected = false;
                    OnDisconnected?.Invoke("Đã ngắt kết nối máy chủ.");
                });
            }
        }

        private async Task NativeSendLoopAsync(ClientWebSocket ws, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
            {
                await _sendSignal.WaitAsync(ct);
                while (_sendQueue.TryDequeue(out string msg))
                {
                    if (ws.State != WebSocketState.Open) return;
                    byte[] bytes = Encoding.UTF8.GetBytes(msg);
                    await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
                }
            }
        }

        private async Task NativeReceiveLoopAsync(ClientWebSocket ws, CancellationToken ct)
        {
            byte[] buffer = new byte[16 * 1024];
            using (MemoryStream ms = new MemoryStream())
            {
                while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
                {
                    ms.SetLength(0);
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                        if (result.MessageType == WebSocketMessageType.Close)
                            return;
                        ms.Write(buffer, 0, result.Count);
                    }
                    while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        string json = Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
                        _mainThreadQueue.Enqueue(() => OnMessageReceived?.Invoke(json));
                    }
                }
            }
        }
#endif
    }
}
