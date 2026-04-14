using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Player registry. Stores names, IDs, scores, and connection state.
/// Pure data store — mutations are initiated by GameCoordinator and game sessions.
/// </summary>
public class PlayerManager : MonoBehaviour
{
    public static PlayerManager Instance { get; private set; }

    private readonly Dictionary<string, PlayerData> _players = new Dictionary<string, PlayerData>();

    /// <summary> Total players (connected + disconnected). </summary>
    public int PlayerCount => _players.Count;

    /// <summary> Only players whose WebSocket is currently connected. </summary>
    public int ActivePlayerCount => _players.Values.Count(p => p.isConnected);

    /// <summary> The first player to join is the host. Auto-promotes on host removal. </summary>
    public string HostPlayerId { get; private set; }

    public bool IsHost(string playerId) => HostPlayerId == playerId;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── Add / Remove ─────────────────────────────

    public string AddPlayer(string name, int tableSide = 0)
    {
        string id = Guid.NewGuid().ToString("N").Substring(0, 8);
        _players[id] = new PlayerData { id = id, name = name, score = 0, isConnected = true, tableSide = tableSide };
        if (string.IsNullOrEmpty(HostPlayerId))
        {
            HostPlayerId = id;
            GameLog.Player($"\"{name}\" is the HOST");
        }
        return id;
    }

    public bool UpdatePlayer(string playerId, string newName, int tableSide)
    {
        if (!_players.TryGetValue(playerId, out var p)) return false;
        p.name = newName;
        p.tableSide = tableSide;
        return true;
    }

    public void RemovePlayer(string playerId)
    {
        _players.Remove(playerId);
        if (HostPlayerId == playerId)
            PromoteNextHost();
    }

    private void PromoteNextHost()
    {
        HostPlayerId = null;
        foreach (var p in _players.Values)
        {
            if (p.isConnected)
            {
                HostPlayerId = p.id;
                GameLog.Player($"\"{p.name}\" promoted to HOST");
                break;
            }
        }
    }

    // ── Connection state ─────────────────────────

    public void DisconnectPlayer(string playerId)
    {
        if (_players.TryGetValue(playerId, out var p))
            p.isConnected = false;
    }

    public void ReconnectPlayer(string playerId)
    {
        if (_players.TryGetValue(playerId, out var p))
            p.isConnected = true;
    }

    public bool IsPlayerConnected(string playerId)
    {
        return _players.TryGetValue(playerId, out var p) && p.isConnected;
    }

    public bool IsPlayerDisconnected(string playerId)
    {
        return _players.TryGetValue(playerId, out var p) && !p.isConnected;
    }

    /// <summary>
    /// Find a disconnected player by name (case-insensitive).
    /// Returns the playerId or null if not found.
    /// Used as a fallback when a returning player doesn't have their original playerId.
    /// </summary>
    public string FindDisconnectedByName(string name)
    {
        foreach (var p in _players.Values)
        {
            if (!p.isConnected && string.Equals(p.name, name, StringComparison.OrdinalIgnoreCase))
                return p.id;
        }
        return null;
    }

    /// <summary>
    /// Remove all disconnected players. Call when returning to Lobby after a game ends.
    /// </summary>
    public void CleanupDisconnectedPlayers()
    {
        var toRemove = _players.Values.Where(p => !p.isConnected).Select(p => p.id).ToList();
        foreach (string id in toRemove)
            _players.Remove(id);

        if (HostPlayerId != null && !_players.ContainsKey(HostPlayerId))
            PromoteNextHost();
    }

    // ── Queries ──────────────────────────────────

    public bool HasPlayer(string playerId) => _players.ContainsKey(playerId);

    public string GetPlayerName(string playerId)
    {
        return _players.TryGetValue(playerId, out var p) ? p.name : "Unknown";
    }

    public int GetTableSide(string playerId)
    {
        return _players.TryGetValue(playerId, out var p) ? p.tableSide : 0;
    }

    public void AddScore(string playerId, int points)
    {
        if (_players.TryGetValue(playerId, out var p))
            p.score += points;
    }

    public void ResetScores()
    {
        foreach (var p in _players.Values)
            p.score = 0;
    }

    public PlayerInfo[] GetAllPlayerInfos()
    {
        var infos = new PlayerInfo[_players.Count];
        int i = 0;
        foreach (var p in _players.Values)
            infos[i++] = new PlayerInfo { id = p.id, name = p.name, score = p.score, tableSide = p.tableSide };
        return infos;
    }

    public List<string> GetAllPlayerIds()
    {
        return new List<string>(_players.Keys);
    }

    public void ClearAll()
    {
        _players.Clear();
        HostPlayerId = null;
    }
}

[Serializable]
public class PlayerData
{
    public string id;
    public string name;
    public int score;
    public bool isConnected;
    public int tableSide;  // 0 = near side, 1 = far side
}
