// CDP transport: single WebSocket connection with id-based send/await and event dispatch.
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace EditorBrowser.Automation
{
    /// <summary>
    /// Owns a single WebSocket connection to a Chrome DevTools Protocol
    /// endpoint — either a per-target page socket (the URL returned by
    /// <c>/json/list</c>) or a browser-level socket. Pure transport: send
    /// pre-built JSON commands and dispatch responses / events.
    ///
    /// <para>Outgoing commands are tagged with an auto-incrementing
    /// <c>id</c>. Incoming messages carrying that <c>id</c> resolve the
    /// matching <see cref="TaskCompletionSource{TResult}"/>. Messages
    /// without an <c>id</c> at the root are treated as events and routed
    /// to <see cref="OnEvent"/> subscribers by method name.</para>
    ///
    /// <para>Thread-safety: the public surface is callable from any thread.
    /// Internally a single receive loop runs on the thread pool; sends are
    /// serialized through an async semaphore so concurrent
    /// <see cref="SendCommandAsync"/> callers don't interleave bytes on the
    /// WebSocket.</para>
    ///
    /// <para>Unity-API-free by design. Safe to use from any background
    /// thread, including during a domain reload prelude — the caller is
    /// responsible for calling <see cref="Dispose"/> in
    /// <c>AssemblyReloadEvents.beforeAssemblyReload</c>.</para>
    /// </summary>
    public sealed class CdpConnection : IDisposable
    {
        private readonly ClientWebSocket _ws = new ClientWebSocket();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        private readonly Dictionary<int, TaskCompletionSource<string>> _pending = new Dictionary<int, TaskCompletionSource<string>>();
        private readonly object _pendingLock = new object();

        private int _nextId;
        private bool _disposed;
        private Task _receiveLoop;

        /// <summary>
        /// Fires when a message without a root-level <c>id</c> arrives.
        /// Arguments: (method, fullJson). Subscribers must not block —
        /// the receive loop dispatches on the same thread.
        /// </summary>
        public event Action<string, string> OnEvent;

        /// <summary>True after a successful <see cref="ConnectAsync"/> and
        /// before the receive loop has observed the socket closing.</summary>
        public bool IsOpen => _ws.State == WebSocketState.Open;

        /// <summary>
        /// Connect to a CDP WebSocket URL. Automatically rewrites
        /// <c>ws://localhost:</c> to <c>ws://127.0.0.1:</c> to avoid the
        /// IPv6→IPv4 DNS fallback that adds a flat ~2s on .NET.
        /// </summary>
        public async Task ConnectAsync(string wsUrl, int timeoutMs = 5000)
        {
            if (string.IsNullOrEmpty(wsUrl)) throw new ArgumentException("wsUrl is required", nameof(wsUrl));
            var fixedUrl = wsUrl.Replace("ws://localhost:", "ws://127.0.0.1:");

            using (var connectCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token))
            {
                connectCts.CancelAfter(timeoutMs);
                await _ws.ConnectAsync(new Uri(fixedUrl), connectCts.Token).ConfigureAwait(false);
            }

            // Spin the receive loop on the thread pool. Hold a reference so
            // Dispose can observe and cancel it.
            _receiveLoop = Task.Run(ReceiveLoop);
        }

        /// <summary>
        /// Send a CDP command and await its JSON response.
        /// <paramref name="paramsJson"/> must be a valid JSON value
        /// (object, array, or primitive) — pass <c>null</c> or empty to
        /// omit the <c>params</c> field. <paramref name="sessionId"/>, if
        /// set, is included so the browser-level WS / pipe transport can
        /// route the command to the right target.
        /// </summary>
        /// <returns>The full JSON response text.</returns>
        /// <exception cref="TimeoutException">No response within
        /// <paramref name="timeoutMs"/>.</exception>
        /// <exception cref="WebSocketException">Connection closed before
        /// the response arrived.</exception>
        public async Task<string> SendCommandAsync(
            string method,
            string paramsJson = null,
            string sessionId = null,
            int timeoutMs = 5000)
        {
            if (string.IsNullOrEmpty(method)) throw new ArgumentException("method is required", nameof(method));

            int id = Interlocked.Increment(ref _nextId);
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_pendingLock) _pending[id] = tcs;

            // Build the JSON manually — paramsJson is opaque to us. Order:
            // id, sessionId (if any), method, params (if any).
            var sb = new StringBuilder(64 + (paramsJson?.Length ?? 0));
            sb.Append("{\"id\":").Append(id);
            if (!string.IsNullOrEmpty(sessionId))
                sb.Append(",\"sessionId\":\"").Append(sessionId).Append('"');
            sb.Append(",\"method\":\"").Append(method).Append('"');
            if (!string.IsNullOrEmpty(paramsJson))
                sb.Append(",\"params\":").Append(paramsJson);
            sb.Append('}');
            var bytes = Encoding.UTF8.GetBytes(sb.ToString());

            await _sendLock.WaitAsync(_cts.Token).ConfigureAwait(false);
            try
            {
                await _ws.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    endOfMessage: true,
                    _cts.Token).ConfigureAwait(false);
            }
            finally
            {
                _sendLock.Release();
            }

            // Race the response against a timeout. If the timeout wins, drop
            // the pending entry so a late-arriving response is discarded
            // rather than leaving a dangling TCS.
            using (var timeoutCts = new CancellationTokenSource(timeoutMs))
            using (timeoutCts.Token.Register(() =>
            {
                lock (_pendingLock) _pending.Remove(id);
                tcs.TrySetException(new TimeoutException($"CDP {method}(id={id}) timed out after {timeoutMs}ms"));
            }))
            {
                return await tcs.Task.ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Escape a string for safe inclusion as a JSON string literal.
        /// Callers building <c>paramsJson</c> by hand will need this for
        /// any user-supplied URL, expression, selector, etc.
        /// </summary>
        public static string EscapeJsonString(string s)
        {
            if (string.IsNullOrEmpty(s)) return s ?? string.Empty;
            var sb = new StringBuilder(s.Length + 8);
            foreach (var c in s)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"':  sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("X4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        private async Task ReceiveLoop()
        {
            var buffer = new byte[16 * 1024];
            var ms = new System.IO.MemoryStream();
            try
            {
                while (!_cts.IsCancellationRequested && _ws.State == WebSocketState.Open)
                {
                    ms.SetLength(0);
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token).ConfigureAwait(false);
                        ms.Write(buffer, 0, result.Count);
                    } while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Close) break;
                    if (result.MessageType != WebSocketMessageType.Text) continue;

                    var json = Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
                    DispatchMessage(json);
                }
            }
            catch
            {
                // Connection died, cancelled, or socket disposed — fall
                // through to the finally block so pending TCSs don't hang.
            }
            finally
            {
                List<TaskCompletionSource<string>> outstanding;
                lock (_pendingLock)
                {
                    outstanding = new List<TaskCompletionSource<string>>(_pending.Values);
                    _pending.Clear();
                }
                foreach (var t in outstanding)
                    t.TrySetException(new WebSocketException("CDP connection closed"));
            }
        }

        private void DispatchMessage(string json)
        {
            int id;
            if (TryParseRootId(json, out id))
            {
                TaskCompletionSource<string> tcs = null;
                lock (_pendingLock)
                {
                    if (_pending.TryGetValue(id, out tcs)) _pending.Remove(id);
                }
                if (tcs != null) tcs.TrySetResult(json);
                return;
            }

            var method = ParseRootStringProp(json, "method");
            if (!string.IsNullOrEmpty(method))
            {
                var handler = OnEvent;
                if (handler != null)
                {
                    try { handler(method, json); }
                    catch { /* don't let a subscriber kill the receive loop */ }
                }
            }
        }

        // Minimal regex parsing — sufficient for CDP's flat root-level keys.
        // A full JSON parse would be more robust but adds dependency weight
        // we don't need for the few fields we read at the root.
        private static readonly Regex s_rootIdRegex =
            new Regex("[{,]\\s*\"id\"\\s*:\\s*(-?\\d+)", RegexOptions.Compiled);

        private static bool TryParseRootId(string json, out int id)
        {
            var m = s_rootIdRegex.Match(json);
            if (m.Success && int.TryParse(m.Groups[1].Value, out id)) return true;
            id = 0;
            return false;
        }

        private static string ParseRootStringProp(string json, string prop)
        {
            // Match "<prop>":"<value>" where value can contain escaped
            // quotes via \". Anchored to a preceding { or , so we don't
            // accidentally pick up nested keys.
            var rx = new Regex("[{,]\\s*\"" + Regex.Escape(prop) + "\"\\s*:\\s*\"((?:\\\\.|[^\"\\\\])*)\"");
            var m = rx.Match(json);
            return m.Success ? m.Groups[1].Value : null;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try { _cts.Cancel(); } catch { }
            try
            {
                // Graceful close attempt — short timeout so Dispose stays snappy.
                if (_ws.State == WebSocketState.Open)
                {
                    var closeTask = _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                    closeTask.Wait(500);
                }
            }
            catch { }
            try { _ws.Dispose(); } catch { }
            try { _receiveLoop?.Wait(500); } catch { }
            try { _sendLock.Dispose(); } catch { }
            try { _cts.Dispose(); } catch { }
        }
    }

    /// <summary>
    /// A logical CDP command context bound to a single
    /// <see cref="CdpConnection"/>. May optionally carry a
    /// <c>sessionId</c> — when set, every outgoing command is wrapped with
    /// <c>"sessionId":"..."</c>, which is how the browser-level WebSocket
    /// (or the future <c>--remote-debugging-pipe</c> transport)
    /// multiplexes commands across multiple targets.
    ///
    /// <para>For the simple per-target WebSocket case (current default —
    /// <c>/json/list</c> hands back a URL that goes straight to one page),
    /// <see cref="SessionId"/> stays null and this class is just a thin
    /// typed wrapper over <see cref="CdpConnection"/>.</para>
    ///
    /// <para>For the future pipe / browser-WS case the caller will:
    /// (1) connect to the browser-level endpoint, (2) send
    /// <c>Target.attachToTarget {flatten:true}</c>, (3) build a
    /// <see cref="CdpSession"/> with the returned sessionId, and (4) use
    /// that session for all target-scoped commands.</para>
    ///
    /// <para>Lives alongside <see cref="CdpConnection"/> in the same
    /// source file to work around a Unity 6 stale-cache quirk that
    /// refused to pick up a sibling .cs file in the same asmdef during
    /// initial creation; can be promoted to its own file in a future
    /// Editor session.</para>
    /// </summary>
    public sealed class CdpSession
    {
        private readonly CdpConnection _connection;
        private readonly string _sessionId;

        /// <summary>
        /// Create a session over the given connection. Pass
        /// <paramref name="sessionId"/> as null for direct per-target
        /// communication, or set it after <c>Target.attachToTarget</c>.
        /// </summary>
        public CdpSession(CdpConnection connection, string sessionId = null)
        {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            _connection = connection;
            _sessionId = sessionId; // null is meaningful (per-target WS)
        }

        /// <summary>The underlying transport.</summary>
        public CdpConnection Connection { get { return _connection; } }

        /// <summary>The CDP sessionId this session targets, or null for
        /// direct per-target WS endpoints.</summary>
        public string SessionId { get { return _sessionId; } }

        /// <summary>
        /// Send a CDP command on this session's connection (with
        /// <see cref="SessionId"/> attached if set) and await the JSON
        /// response. See <see cref="CdpConnection.SendCommandAsync"/> for
        /// parameter contract and exception semantics.
        /// </summary>
        public Task<string> SendAsync(string method, string paramsJson = null, int timeoutMs = 5000)
        {
            return _connection.SendCommandAsync(method, paramsJson, _sessionId, timeoutMs);
        }
    }
}
