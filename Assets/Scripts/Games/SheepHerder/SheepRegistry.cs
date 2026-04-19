using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scene-level registry used by <see cref="Sheep"/> agents to query their neighbours and by the
/// Sheep Herder game to enumerate shepherds. Centralising the lists here avoids every sheep
/// doing its own FindObjectsOfType each FixedUpdate, which would scale O(n²) quickly.
///
/// One instance per scene; <see cref="SheepHerderManager"/> creates it during OnSessionStart.
/// Lists are kept tight — sheep unregister themselves when scored or destroyed.
/// </summary>
public class SheepRegistry : MonoBehaviour
{
    public static SheepRegistry Instance { get; private set; }

    private readonly List<Sheep> _sheep = new List<Sheep>();
    private readonly List<Transform> _shepherds = new List<Transform>();

    public IReadOnlyList<Sheep> Sheep => _sheep;
    public IReadOnlyList<Transform> Shepherds => _shepherds;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void RegisterSheep(Sheep s) { if (s != null && !_sheep.Contains(s)) _sheep.Add(s); }
    public void UnregisterSheep(Sheep s) { _sheep.Remove(s); }

    public void RegisterShepherd(Transform t) { if (t != null && !_shepherds.Contains(t)) _shepherds.Add(t); }
    public void UnregisterShepherd(Transform t) { _shepherds.Remove(t); }

    public int ActiveSheepCount
    {
        get
        {
            int n = 0;
            foreach (var s in _sheep) if (s != null && !s.IsScored) n++;
            return n;
        }
    }
}
