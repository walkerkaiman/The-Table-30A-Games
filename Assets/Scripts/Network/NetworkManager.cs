using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

/// <summary>
/// Pure transport layer — game-agnostic.
/// Routes join/rejoin via dedicated events; all other message types are forwarded
/// as generic GameMessageReceived events for GameCoordinator to dispatch.
/// </summary>
public class NetworkManager : MonoBehaviour
{
    public static NetworkManager Instance { get; private set; }

    [Header("Server Settings")]
    [SerializeField] private int port = 7777;

    [Header("Discovery")]
    [SerializeField] private string gameName = "Game 1";

    private GameServer _server;
    private GameDiscovery _discovery;

    private readonly ConcurrentQueue<Action> _mainThreadQueue = new ConcurrentQueue<Action>();

    private readonly ConcurrentDictionary<string, string> _connToPlayer = new ConcurrentDictionary<string, string>();
    private readonly ConcurrentDictionary<string, string> _playerToConn = new ConcurrentDictionary<string, string>();
    private readonly ConcurrentDictionary<string, WebSocketConnection> _pendingConns = new ConcurrentDictionary<string, WebSocketConnection>();

    public string LocalIP { get; private set; }
    public int Port => port;

    // ── Unity Lifecycle ──────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        LocalIP = GetLocalIPAddress();
    }

    private void Start()
    {
        SubscribeToGameEvents();

        _discovery = new GameDiscovery();
        _discovery.Start();

        string webRoot = System.IO.Path.Combine(Application.streamingAssetsPath, "WebApp");

        _server = new GameServer();
        _server.Discovery = _discovery;
        _server.OnClientConnected += OnWsConnected;
        _server.OnClientDisconnected += OnWsDisconnected;
        _server.OnMessageReceived += OnWsMessage;
        _server.Start(port, webRoot);

        GameLog.Server($"HTTP + WebSocket server started on port {port}");
        GameLog.Server($"Serving web app from: {webRoot}");
        GameLog.Net($"Local IP: {LocalIP}");
        GameLog.Net($"Discovery broadcasting as \"{gameName}\"");

        UpdateDiscoveryInfo();
    }

    private void Update()
    {
        while (_mainThreadQueue.TryDequeue(out Action action))
        {
            try { action(); }
            catch (Exception ex) { GameLog.Error($"Main-thread queue error: {ex}"); }
        }
    }

    private void OnDestroy()
    {
        UnsubscribeFromGameEvents();
        _server?.Stop();
        _discovery?.Stop();
        GameLog.Server("Server stopped");
    }

    // ── Event Subscriptions ──────────────────────

    private void SubscribeToGameEvents()
    {
        GameEvents.JoinAccepted += HandleJoinAccepted;
        GameEvents.JoinRejected += HandleJoinRejected;
        GameEvents.RejoinAccepted += HandleRejoinAccepted;
        GameEvents.RejoinRejected += HandleRejoinRejected;
        GameEvents.BroadcastMessage += HandleBroadcast;
        GameEvents.SendToPlayer += HandleSendToPlayer;
        GameEvents.PlayerListChanged += HandlePlayerListChanged;
    }

    private void UnsubscribeFromGameEvents()
    {
        GameEvents.JoinAccepted -= HandleJoinAccepted;
        GameEvents.JoinRejected -= HandleJoinRejected;
        GameEvents.RejoinAccepted -= HandleRejoinAccepted;
        GameEvents.RejoinRejected -= HandleRejoinRejected;
        GameEvents.BroadcastMessage -= HandleBroadcast;
        GameEvents.SendToPlayer -= HandleSendToPlayer;
        GameEvents.PlayerListChanged -= HandlePlayerListChanged;
    }

    // ── WebSocket Callbacks ──────────────────────

    private void OnWsConnected(WebSocketConnection conn)
    {
        _mainThreadQueue.Enqueue(() => GameLog.Net($"WebSocket opened: {conn.Id}"));
    }

    private void OnWsDisconnected(WebSocketConnection conn)
    {
        _mainThreadQueue.Enqueue(() =>
        {
            _pendingConns.TryRemove(conn.Id, out _);

            if (_connToPlayer.TryRemove(conn.Id, out string playerId))
            {
                _playerToConn.TryRemove(playerId, out _);
                GameLog.Net($"WebSocket closed: {conn.Id} (player: {playerId})");
                GameEvents.FirePlayerDisconnected(playerId);
            }
            else
            {
                GameLog.Net($"WebSocket closed: {conn.Id} (no session)");
            }
        });
    }

    private void OnWsMessage(WebSocketConnection conn, string json)
    {
        _mainThreadQueue.Enqueue(() => RouteIncomingMessage(conn, json));
    }

    // ── Message Routing ──────────────────────────

    private void RouteIncomingMessage(WebSocketConnection conn, string json)
    {
        BaseMessage baseMsg;
        try { baseMsg = JsonUtility.FromJson<BaseMessage>(json); }
        catch
        {
            GameLog.Warn($"Unparseable message from connection {conn.Id}");
            return;
        }

        switch (baseMsg.type)
        {
            case "join":
                var joinMsg = JsonUtility.FromJson<JoinMessage>(json);
                GameLog.Net($"Join request: \"{joinMsg.name}\" room={joinMsg.roomCode} (conn: {conn.Id})");
                _pendingConns[conn.Id] = conn;
                GameEvents.FireJoinRequested(conn.Id, joinMsg.name, joinMsg.roomCode);
                break;

            case "rejoin":
                var rejoinMsg = JsonUtility.FromJson<RejoinMessage>(json);
                GameLog.Net($"Rejoin request: \"{rejoinMsg.name}\" playerId={rejoinMsg.playerId} (conn: {conn.Id})");
                _pendingConns[conn.Id] = conn;
                GameEvents.FireRejoinRequested(conn.Id, rejoinMsg.playerId, rejoinMsg.name);
                break;

            default:
                if (TryGetPlayerId(conn, out string playerId))
                    GameEvents.FireGameMessageReceived(playerId, baseMsg.type, json);
                break;
        }
    }

    private bool TryGetPlayerId(WebSocketConnection conn, out string playerId)
    {
        return _connToPlayer.TryGetValue(conn.Id, out playerId);
    }

    // ── Outbound Handlers ────────────────────────

    private void HandleJoinAccepted(string connId, string playerId, string playerName, string roomCode)
    {
        if (!_pendingConns.TryRemove(connId, out WebSocketConnection conn)) return;
        _connToPlayer[connId] = playerId;
        _playerToConn[playerId] = connId;
        conn.Send(JsonUtility.ToJson(new WelcomeMessage { playerId = playerId, roomCode = roomCode }));
        GameLog.Net($"Sent welcome to \"{playerName}\" ({playerId})");
        UpdateDiscoveryInfo();
    }

    private void HandleJoinRejected(string connId, string errorMessage)
    {
        if (!_pendingConns.TryRemove(connId, out WebSocketConnection conn)) return;
        conn.Send(JsonUtility.ToJson(new ErrorMessage { message = errorMessage }));
    }

    private void HandleRejoinAccepted(string connId, string playerId, string playerName)
    {
        if (!_pendingConns.TryRemove(connId, out WebSocketConnection conn)) return;
        if (_playerToConn.TryRemove(playerId, out string oldConnId))
            _connToPlayer.TryRemove(oldConnId, out _);
        _connToPlayer[connId] = playerId;
        _playerToConn[playerId] = connId;
        conn.Send(JsonUtility.ToJson(new RejoinSuccessMessage { playerId = playerId }));
        GameLog.Net($"Sent rejoin_success to \"{playerName}\" ({playerId})");
        UpdateDiscoveryInfo();
    }

    private void HandleRejoinRejected(string connId, string errorMessage)
    {
        if (!_pendingConns.TryRemove(connId, out WebSocketConnection conn)) return;
        conn.Send(JsonUtility.ToJson(new ErrorMessage { message = errorMessage }));
    }

    private void HandleBroadcast(string json)
    {
        _server?.Broadcast(json);
    }

    private void HandleSendToPlayer(string playerId, string json)
    {
        if (_playerToConn.TryGetValue(playerId, out string connId))
            _server?.SendTo(connId, json);
    }

    private void HandlePlayerListChanged()
    {
        var msg = new PlayerListMessage
        {
            players = PlayerManager.Instance.GetAllPlayerInfos(),
            hostId = PlayerManager.Instance.HostPlayerId ?? ""
        };
        _server?.Broadcast(JsonUtility.ToJson(msg));
        GameLog.Net($"Broadcast player list ({PlayerManager.Instance.ActivePlayerCount} active / {PlayerManager.Instance.PlayerCount} total)");
        UpdateDiscoveryInfo();
    }

    // ── Discovery ────────────────────────────────

    private void UpdateDiscoveryInfo()
    {
        if (_discovery == null || GameCoordinator.Instance == null) return;
        _discovery.UpdateGameInfo(
            gameName,
            LocalIP,
            port,
            GameCoordinator.Instance.RoomCode,
            PlayerManager.Instance != null ? PlayerManager.Instance.PlayerCount : 0,
            GameCoordinator.Instance.CurrentState.ToString()
        );
    }

    // ── Helpers ──────────────────────────────────

    private static string GetLocalIPAddress()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                    return ip.ToString();
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Could not determine local IP: {ex.Message}");
        }
        return "127.0.0.1";
    }
}
