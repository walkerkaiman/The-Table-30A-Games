using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

/// <summary>
/// LAN game discovery via UDP broadcast.
/// Each server periodically broadcasts its presence; each server also listens for others.
/// The aggregated list (self + others) is served to web clients via GameServer's /api/games route.
/// </summary>
public class GameDiscovery
{
    private readonly string _serverId = Guid.NewGuid().ToString("N").Substring(0, 8);

    private UdpClient _sender;
    private UdpClient _receiver;
    private Thread _broadcastThread;
    private Thread _listenThread;
    private volatile bool _running;

    private volatile string _broadcastPayload = "";
    private volatile DiscoveredGameInfo _selfInfo;

    private readonly ConcurrentDictionary<string, DiscoveredGameEntry> _others
        = new ConcurrentDictionary<string, DiscoveredGameEntry>();

    private const int DISCOVERY_PORT = 47777;
    private const int BROADCAST_INTERVAL_MS = 2000;
    private const long STALE_THRESHOLD_MS = 6000;

    // ── Lifecycle ────────────────────────────────

    public void Start()
    {
        _running = true;

        try
        {
            _sender = new UdpClient();
            _sender.EnableBroadcast = true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Discovery] Could not create broadcast sender: {ex.Message}");
        }

        try
        {
            _receiver = new UdpClient();
            _receiver.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _receiver.Client.Bind(new IPEndPoint(IPAddress.Any, DISCOVERY_PORT));
            _receiver.EnableBroadcast = true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Discovery] Could not bind listener on port {DISCOVERY_PORT}: {ex.Message}");
        }

        _broadcastThread = new Thread(BroadcastLoop) { IsBackground = true };
        _broadcastThread.Start();

        if (_receiver != null)
        {
            _listenThread = new Thread(ListenLoop) { IsBackground = true };
            _listenThread.Start();
        }

        Debug.Log($"[Discovery] Started (serverId: {_serverId}, port: {DISCOVERY_PORT})");
    }

    public void Stop()
    {
        _running = false;
        try { _sender?.Close(); } catch { }
        try { _receiver?.Close(); } catch { }
    }

    // ── Game Info (called from main thread) ──────

    public void UpdateGameInfo(string gameName, string ip, int port, string roomCode, int playerCount, string state)
    {
        _selfInfo = new DiscoveredGameInfo
        {
            gameName = gameName,
            ip = ip,
            port = port,
            roomCode = roomCode,
            playerCount = playerCount,
            state = state
        };

        var broadcast = new DiscoveryBroadcast
        {
            serverId = _serverId,
            gameName = gameName,
            ip = ip,
            port = port,
            roomCode = roomCode,
            playerCount = playerCount,
            state = state
        };
        _broadcastPayload = JsonUtility.ToJson(broadcast);
    }

    /// <summary>
    /// Returns JSON for the /api/games endpoint. Includes self and all non-stale discovered servers.
    /// Safe to call from any thread.
    /// </summary>
    public string GetAllGamesJson()
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var list = new List<DiscoveredGameInfo>();

        if (_selfInfo != null)
            list.Add(_selfInfo);

        foreach (var kvp in _others)
        {
            if (now - kvp.Value.lastSeenMs > STALE_THRESHOLD_MS)
            {
                _others.TryRemove(kvp.Key, out _);
                continue;
            }
            list.Add(kvp.Value.info);
        }

        var wrapper = new DiscoveredGamesWrapper { games = list.ToArray() };
        return JsonUtility.ToJson(wrapper);
    }

    /// <summary>
    /// Returns JSON for the /api/info endpoint (this server only).
    /// </summary>
    public string GetSelfInfoJson()
    {
        return _selfInfo != null ? JsonUtility.ToJson(_selfInfo) : "{}";
    }

    // ── Background Threads ───────────────────────

    private void BroadcastLoop()
    {
        var endpoint = new IPEndPoint(IPAddress.Broadcast, DISCOVERY_PORT);
        while (_running)
        {
            try
            {
                string payload = _broadcastPayload;
                if (!string.IsNullOrEmpty(payload) && _sender != null)
                {
                    byte[] data = Encoding.UTF8.GetBytes(payload);
                    _sender.Send(data, data.Length, endpoint);
                }
            }
            catch (ObjectDisposedException) { break; }
            catch (Exception) { }

            Thread.Sleep(BROADCAST_INTERVAL_MS);
        }
    }

    private void ListenLoop()
    {
        while (_running)
        {
            try
            {
                IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = _receiver.Receive(ref remoteEP);
                string json = Encoding.UTF8.GetString(data);

                var broadcast = JsonUtility.FromJson<DiscoveryBroadcast>(json);
                if (broadcast == null || broadcast.serverId == _serverId) continue;

                _others[broadcast.serverId] = new DiscoveredGameEntry
                {
                    info = new DiscoveredGameInfo
                    {
                        gameName = broadcast.gameName,
                        ip = broadcast.ip,
                        port = broadcast.port,
                        roomCode = broadcast.roomCode,
                        playerCount = broadcast.playerCount,
                        state = broadcast.state
                    },
                    lastSeenMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                };
            }
            catch (ObjectDisposedException) { break; }
            catch (SocketException) { if (!_running) break; }
            catch (Exception) { }
        }
    }

    // ── Internal ─────────────────────────────────

    private class DiscoveredGameEntry
    {
        public DiscoveredGameInfo info;
        public long lastSeenMs;
    }
}
