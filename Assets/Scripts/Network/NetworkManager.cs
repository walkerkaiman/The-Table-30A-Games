using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
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

    [Header("Network Adapter Selection")]
    [Tooltip("Optional: name (or partial name) of the OS network adapter to bind the host's IP to, " +
             "e.g. \"Ethernet\", \"Wi-Fi\", or \"Realtek\". This is the NIC NAME on this PC, " +
             "NOT the WiFi SSID you want phones to join (that's QRCodeDisplay.wifiSSID). " +
             "When set, GetLocalIPAddress prefers an adapter whose Name or Description contains this string (case-insensitive). " +
             "Leave blank to auto-pick the first routable IPv4. Use the component's right-click ▸ Log Network Adapters " +
             "context menu to see candidate names.")]
    public string networkName = "";

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
            case MessageTypes.Join:
                var joinMsg = JsonUtility.FromJson<JoinMessage>(json);
                GameLog.Net($"Join request: \"{joinMsg.name}\" room={joinMsg.roomCode} side={joinMsg.tableSide} playerId={joinMsg.playerId ?? ""} (conn: {conn.Id})");
                _pendingConns[conn.Id] = conn;
                GameEvents.FireJoinRequested(conn.Id, joinMsg.name, joinMsg.roomCode, joinMsg.tableSide, joinMsg.playerId ?? "");
                break;

            case MessageTypes.Rejoin:
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
        if (_playerToConn.TryRemove(playerId, out string oldConnId))
            _connToPlayer.TryRemove(oldConnId, out _);
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

    /// <summary>
    /// Recompute <see cref="LocalIP"/> using the current <see cref="networkName"/>
    /// value. Call this if you edit the field at runtime; the server keeps listening
    /// on all interfaces either way, but any subsequently-generated QR codes will
    /// embed the refreshed IP.
    /// </summary>
    public void RefreshLocalIP()
    {
        LocalIP = GetLocalIPAddress();
        GameLog.Net($"NetworkManager: LocalIP refreshed → {LocalIP}");
        UpdateDiscoveryInfo();
    }

    /// <summary>
    /// Logs every Up IPv4 adapter to the console so you can see what to type into
    /// <see cref="networkName"/>. Helpful when the wrong adapter is being picked.
    /// </summary>
    [ContextMenu("Log Network Adapters")]
    public void LogAllAdapters()
    {
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                var props = nic.GetIPProperties();
                foreach (var addr in props.UnicastAddresses)
                {
                    if (addr.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    if (IPAddress.IsLoopback(addr.Address)) continue;
                    int gateways = props.GatewayAddresses.Count;
                    GameLog.Net($"  • [{nic.NetworkInterfaceType}] name=\"{nic.Name}\" desc=\"{nic.Description}\" ip={addr.Address} gateways={gateways}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"NetworkManager.LogAllAdapters failed: {ex.Message}");
        }
    }

    private string GetLocalIPAddress()
    {
        // First pass: if networkName is set, look for a matching adapter and return
        // the first IPv4 unicast address bound to it. This lets the user steer the
        // host away from VPN/virtual adapters when their PC has multiple NICs.
        string preferred = TryGetIPFromNamedAdapter(networkName);
        if (!string.IsNullOrEmpty(preferred)) return preferred;

        if (!string.IsNullOrEmpty(networkName))
            Debug.LogWarning($"NetworkManager: no Up adapter matching \"{networkName}\" found — falling back to auto-pick. " +
                             "Use the \"Log Network Adapters\" context menu on this component to list candidates.");

        // Second pass: pick the first Up IPv4 adapter that actually has a default
        // gateway. This filters out Hyper-V/VPN/WSL virtual adapters that are Up
        // but unrouted, which are what the old DNS-based pick used to grab.
        string routable = TryGetFirstRoutableIPv4();
        if (!string.IsNullOrEmpty(routable)) return routable;

        // Last-resort fallback: legacy DNS-based pick.
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

    private static string TryGetIPFromNamedAdapter(string nameFilter)
    {
        if (string.IsNullOrWhiteSpace(nameFilter)) return null;

        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                bool nameMatch =
                    nic.Name.IndexOf(nameFilter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    nic.Description.IndexOf(nameFilter, StringComparison.OrdinalIgnoreCase) >= 0;
                if (!nameMatch) continue;

                var props = nic.GetIPProperties();
                foreach (var addr in props.UnicastAddresses)
                {
                    if (addr.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    if (IPAddress.IsLoopback(addr.Address)) continue;
                    GameLog.Net($"NetworkManager: matched adapter \"{nic.Name}\" ({nic.Description}) → {addr.Address}");
                    return addr.Address.ToString();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"NetworkManager: adapter lookup for \"{nameFilter}\" failed: {ex.Message}");
        }
        return null;
    }

    private static string TryGetFirstRoutableIPv4()
    {
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                var props = nic.GetIPProperties();
                if (props.GatewayAddresses.Count == 0) continue; // skip unrouted virtual adapters

                foreach (var addr in props.UnicastAddresses)
                {
                    if (addr.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    if (IPAddress.IsLoopback(addr.Address)) continue;
                    GameLog.Net($"NetworkManager: auto-picked routable adapter \"{nic.Name}\" ({nic.Description}) → {addr.Address}");
                    return addr.Address.ToString();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"NetworkManager: routable lookup failed: {ex.Message}");
        }
        return null;
    }
}
