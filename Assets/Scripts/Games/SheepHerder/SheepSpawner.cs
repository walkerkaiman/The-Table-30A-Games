using UnityEngine;

/// <summary>
/// Scatters a configurable number of sheep within a 2D rectangle on the XY plane (top-down 2D).
/// Used by <see cref="SheepHerderManager"/> at session start and tidies them up at session end.
///
/// Author the sheep prefab in Unity (Rigidbody2D + Collider2D + <see cref="Sheep"/> + a sprite) and
/// drop it into the <c>sheepPrefab</c> slot. The spawner registers each instance with the
/// <see cref="SheepRegistry"/> so flocking queries see them immediately.
/// </summary>
public class SheepSpawner : MonoBehaviour
{
    [Header("Prefab")]
    [SerializeField] private Sheep sheepPrefab;

    [Header("Count")]
    [SerializeField] private int sheepCount = 20;

    [Header("Spawn Area (XY plane, top-down 2D)")]
    [SerializeField] private Vector2 spawnCenter = Vector2.zero;
    [SerializeField] private Vector2 spawnSize = new Vector2(10f, 10f);
    [Tooltip("Z depth to place sheep on. Usually 0 for 2D; tweak if you're stacking layers.")]
    [SerializeField] private float spawnZ = 0f;

    [Header("Jitter")]
    [Tooltip("Random heading applied to each sheep so they don't all start identical.")]
    [SerializeField] private bool randomizeFacing = true;

    private readonly System.Collections.Generic.List<Sheep> _spawned = new();

    /// <summary>Spawn all configured sheep. Returns the number spawned.</summary>
    public int SpawnAll()
    {
        DespawnAll();

        if (sheepPrefab == null)
        {
            GameLog.Warn("SheepSpawner: sheepPrefab is not assigned.");
            return 0;
        }

        var registry = SheepRegistry.Instance;
        for (int i = 0; i < sheepCount; i++)
        {
            Vector3 pos = RandomPointInArea();
            Quaternion rot = randomizeFacing
                ? Quaternion.Euler(0f, 0f, Random.Range(0f, 360f))
                : Quaternion.identity;

            Sheep sheep = Instantiate(sheepPrefab, pos, rot, transform);
            sheep.name = $"Sheep_{i:00}";
            _spawned.Add(sheep);
            registry?.RegisterSheep(sheep);
        }

        return _spawned.Count;
    }

    public void DespawnAll()
    {
        foreach (var s in _spawned)
        {
            if (s != null)
            {
                SheepRegistry.Instance?.UnregisterSheep(s);
                Destroy(s.gameObject);
            }
        }
        _spawned.Clear();
    }

    private Vector3 RandomPointInArea()
    {
        float x = Random.Range(-spawnSize.x * 0.5f, spawnSize.x * 0.5f);
        float y = Random.Range(-spawnSize.y * 0.5f, spawnSize.y * 0.5f);
        return new Vector3(spawnCenter.x + x, spawnCenter.y + y, spawnZ);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 0.9f, 0.4f, 0.25f);
        Gizmos.DrawWireCube(new Vector3(spawnCenter.x, spawnCenter.y, spawnZ),
                            new Vector3(spawnSize.x, spawnSize.y, 0.1f));
    }
#endif
}
