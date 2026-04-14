using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using UnityEngine;

/// <summary>
/// Combined HTTP file server + WebSocket server on a single port.
/// HTTP requests serve the web app from StreamingAssets/WebApp.
/// Requests with "Upgrade: websocket" are upgraded to persistent WebSocket connections.
/// API routes (/api/games, /api/info) are handled before static file serving.
/// </summary>
public class GameServer
{
    private TcpListener _listener;
    private Thread _listenThread;
    private volatile bool _running;
    private string _webRootPath;

    private readonly ConcurrentDictionary<string, WebSocketConnection> _connections
        = new ConcurrentDictionary<string, WebSocketConnection>();

    public event Action<WebSocketConnection> OnClientConnected;
    public event Action<WebSocketConnection> OnClientDisconnected;
    public event Action<WebSocketConnection, string> OnMessageReceived;

    /// <summary> Set by NetworkManager so API routes can access discovery data. </summary>
    public GameDiscovery Discovery { get; set; }

    private const string WS_GUID = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

    // ── Lifecycle ────────────────────────────────

    public void Start(int port, string webRootPath)
    {
        _webRootPath = webRootPath;
        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();
        _running = true;

        _listenThread = new Thread(ListenLoop) { IsBackground = true };
        _listenThread.Start();

        Debug.Log($"[Server] Listening on port {port}");
        Debug.Log($"[Server] Serving web files from: {_webRootPath}");
    }

    public void Stop()
    {
        _running = false;

        try { _listener?.Stop(); } catch { }

        foreach (var conn in _connections.Values)
            conn.Close();
        _connections.Clear();
    }

    // ── Accept loop ──────────────────────────────

    private void ListenLoop()
    {
        while (_running)
        {
            try
            {
                TcpClient client = _listener.AcceptTcpClient();
                ThreadPool.QueueUserWorkItem(_ => HandleNewConnection(client));
            }
            catch (SocketException)
            {
                if (_running) Debug.Log("[Server] Accept interrupted");
            }
        }
    }

    private void HandleNewConnection(TcpClient client)
    {
        try
        {
            client.ReceiveTimeout = 5000;
            NetworkStream stream = client.GetStream();

            byte[] buffer = new byte[4096];
            int bytesRead = stream.Read(buffer, 0, buffer.Length);
            if (bytesRead == 0) { client.Close(); return; }

            string request = Encoding.UTF8.GetString(buffer, 0, bytesRead);

            if (IsWebSocketUpgrade(request))
            {
                client.ReceiveTimeout = 0;
                HandleWebSocketUpgrade(client, stream, request);
            }
            else
            {
                HandleHttpRequest(client, stream, request);
            }
        }
        catch (Exception ex)
        {
            Debug.Log($"[Server] Connection handling error: {ex.Message}");
            try { client.Close(); } catch { }
        }
    }

    // ── HTTP request handling ────────────────────

    private void HandleHttpRequest(TcpClient client, NetworkStream stream, string request)
    {
        try
        {
            string path = ParseRequestPath(request);

            if (path.StartsWith("/api/"))
            {
                HandleApiRequest(stream, path);
                return;
            }

            string filePath = Path.Combine(_webRootPath, path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

            byte[] body;
            string status;
            string contentType;

            if (File.Exists(filePath))
            {
                body = File.ReadAllBytes(filePath);
                status = "200 OK";
                contentType = GetMimeType(filePath);
            }
            else
            {
                body = Encoding.UTF8.GetBytes("404 Not Found");
                status = "404 Not Found";
                contentType = "text/plain";
            }

            string header = $"HTTP/1.1 {status}\r\n" +
                            $"Content-Type: {contentType}\r\n" +
                            $"Content-Length: {body.Length}\r\n" +
                            "Connection: close\r\n" +
                            "Cache-Control: no-cache\r\n\r\n";

            byte[] headerBytes = Encoding.UTF8.GetBytes(header);
            stream.Write(headerBytes, 0, headerBytes.Length);
            stream.Write(body, 0, body.Length);
        }
        finally
        {
            client.Close();
        }
    }

    private void HandleApiRequest(NetworkStream stream, string path)
    {
        string json;

        switch (path)
        {
            case "/api/games":
                json = Discovery?.GetAllGamesJson() ?? "{\"games\":[]}";
                break;

            case "/api/info":
                json = Discovery?.GetSelfInfoJson() ?? "{}";
                break;

            default:
                json = "{\"error\":\"not found\"}";
                break;
        }

        byte[] body = Encoding.UTF8.GetBytes(json);
        string header = "HTTP/1.1 200 OK\r\n" +
                        "Content-Type: application/json; charset=utf-8\r\n" +
                        $"Content-Length: {body.Length}\r\n" +
                        "Access-Control-Allow-Origin: *\r\n" +
                        "Connection: close\r\n" +
                        "Cache-Control: no-cache\r\n\r\n";

        byte[] headerBytes = Encoding.UTF8.GetBytes(header);
        stream.Write(headerBytes, 0, headerBytes.Length);
        stream.Write(body, 0, body.Length);
    }

    private static string ParseRequestPath(string request)
    {
        int start = request.IndexOf(' ') + 1;
        int end = request.IndexOf(' ', start);
        string path = (start > 0 && end > start) ? request.Substring(start, end - start) : "/";

        int q = path.IndexOf('?');
        if (q >= 0) path = path.Substring(0, q);

        if (path == "/") path = "/index.html";
        return path;
    }

    private static string GetMimeType(string path)
    {
        switch (Path.GetExtension(path).ToLowerInvariant())
        {
            case ".html": return "text/html; charset=utf-8";
            case ".css":  return "text/css; charset=utf-8";
            case ".js":   return "application/javascript; charset=utf-8";
            case ".json": return "application/json; charset=utf-8";
            case ".png":  return "image/png";
            case ".jpg":
            case ".jpeg": return "image/jpeg";
            case ".gif":  return "image/gif";
            case ".svg":  return "image/svg+xml";
            case ".ico":  return "image/x-icon";
            case ".mp4":  return "video/mp4";
            case ".webm": return "video/webm";
            case ".mov":  return "video/quicktime";
            case ".webp": return "image/webp";
            default:      return "application/octet-stream";
        }
    }

    // ── WebSocket upgrade ────────────────────────

    private static bool IsWebSocketUpgrade(string request)
    {
        return request.IndexOf("Upgrade: websocket", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void HandleWebSocketUpgrade(TcpClient client, NetworkStream stream, string request)
    {
        string key = ExtractHeader(request, "Sec-WebSocket-Key");
        if (key == null) { client.Close(); return; }

        string acceptHash;
        using (var sha1 = SHA1.Create())
        {
            acceptHash = Convert.ToBase64String(
                sha1.ComputeHash(Encoding.UTF8.GetBytes(key + WS_GUID))
            );
        }

        string response =
            "HTTP/1.1 101 Switching Protocols\r\n" +
            "Upgrade: websocket\r\n" +
            "Connection: Upgrade\r\n" +
            $"Sec-WebSocket-Accept: {acceptHash}\r\n\r\n";

        byte[] responseBytes = Encoding.UTF8.GetBytes(response);
        stream.Write(responseBytes, 0, responseBytes.Length);

        var connection = new WebSocketConnection(client, stream);
        _connections[connection.Id] = connection;

        connection.OnMessage += msg => OnMessageReceived?.Invoke(connection, msg);
        connection.OnDisconnected += () =>
        {
            _connections.TryRemove(connection.Id, out _);
            OnClientDisconnected?.Invoke(connection);
        };

        Debug.Log($"[Server] WebSocket client connected: {connection.Id}");
        OnClientConnected?.Invoke(connection);

        connection.StartReceiving();
    }

    private static string ExtractHeader(string request, string headerName)
    {
        foreach (string line in request.Split(new[] { "\r\n" }, StringSplitOptions.None))
        {
            if (line.StartsWith(headerName + ":", StringComparison.OrdinalIgnoreCase))
                return line.Substring(line.IndexOf(':') + 1).Trim();
        }
        return null;
    }

    // ── Broadcasting ─────────────────────────────

    public void Broadcast(string json)
    {
        foreach (var conn in _connections.Values)
            conn.Send(json);
    }

    public void SendTo(string connectionId, string json)
    {
        if (_connections.TryGetValue(connectionId, out var conn))
            conn.Send(json);
    }
}
