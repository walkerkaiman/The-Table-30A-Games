using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scene-level registry for all active <see cref="Explorer"/> instances.
/// Mirrors the pattern used by <see cref="SheepRegistry"/> in Sheep Herder.
///
/// Provides fast lookups needed by:
///   • <see cref="PressurePlate"/> — which explorers are on a plate.
///   • <see cref="Trap"/> — non-downed explorers entering a trigger.
///   • <see cref="DownedController"/> — nearby standing explorers for revive proximity.
///   • <see cref="FogOfWar"/> — player transform positions each frame.
///
/// One instance per scene. TreasureHunterManager creates it during Awake if none exists.
/// </summary>
public class ExplorerRegistry : MonoBehaviour
{
    public static ExplorerRegistry Instance { get; private set; }

    private readonly List<Explorer> _explorers = new List<Explorer>();
    private readonly Dictionary<string, Explorer> _byPlayerId = new Dictionary<string, Explorer>();

    public IReadOnlyList<Explorer> AllExplorers => _explorers;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Register(Explorer e)
    {
        if (e == null) return;
        if (!_explorers.Contains(e)) _explorers.Add(e);
        if (!string.IsNullOrEmpty(e.PlayerId)) _byPlayerId[e.PlayerId] = e;
    }

    public void Unregister(Explorer e)
    {
        if (e == null) return;
        _explorers.Remove(e);
        if (!string.IsNullOrEmpty(e.PlayerId)) _byPlayerId.Remove(e.PlayerId);
    }

    public Explorer GetByPlayerId(string playerId)
    {
        _byPlayerId.TryGetValue(playerId, out var e);
        return e;
    }

    public int ActiveCount
    {
        get
        {
            int n = 0;
            foreach (var e in _explorers) if (e != null && !e.IsEscaped) n++;
            return n;
        }
    }

    public int EscapedCount
    {
        get
        {
            int n = 0;
            foreach (var e in _explorers) if (e != null && e.IsEscaped) n++;
            return n;
        }
    }
}
