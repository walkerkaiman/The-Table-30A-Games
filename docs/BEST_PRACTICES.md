# The Table 30A — Best Practices

Coding standards and conventions for the party-game Unity project.
Follow these when adding new games, fixing bugs, or refactoring.

---

## 1. Project Layout & Namespaces

```
Assets/Scripts/
  Game/             Core framework — GameCoordinator, PlayerManager, IGameSession, etc.
  Network/          GameServer, WebSocketConnection, GameDiscovery, MessageModels
  Input/            PlayerInputRelay, JoystickInputRelay, AcceleratedMover, identity
  Display/          GameTableDisplay, LobbyDisplay, MirroredTableCamera, QRCodeDisplay
  Utilities/        Leaderboard, ListExtensions, GameContentLoader, QRCodeEncoder
  Games/
    Quiplash/       QuiplashManager + RoundManager + PromptDatabase
    Fibbage/        FibbageManager
    Pong/           PongManager, PongGoalZone, PowerupManager, SplitHandler
    SheepHerder/    SheepHerderManager, Sheep, Shepherd, SheepRegistry, etc.
    Telephone/      TelephoneManager, TelephoneTableDisplay
    HotPotato/      HotPotatoManager
    CaptionContest/ CaptionContestManager
    PriceIsClose/   PriceIsCloseManager
```

**Dependency direction:** Games depend on Core, Network, Input, and Display.
Core must never reference a specific game namespace.

---

## 2. Singletons Policy

**Allowed singletons** (persistent across scenes, `DontDestroyOnLoad`):
- `NetworkManager`
- `GameCoordinator`
- `PlayerManager`

**Per-scene singletons** (destroyed with their scene):
- `PongManager.Instance`
- `SheepHerderManager.Instance`
- `SheepRegistry.Instance`

**Not singletons — must be plain C# services owned by their manager:**
- Game-specific round data (e.g., prompts, answers, votes)
- Content loaders (`GameContentLoader<T>`)

When a class doesn't need `MonoBehaviour` lifecycle (Update, coroutines, serialized fields),
make it a plain C# class instead.

---

## 3. Event Bus (`GameEvents`)

- **Main-thread only.** All events fire on the Unity main thread (NetworkManager
  marshals socket callbacks via `ConcurrentQueue<Action>` drained in `Update`).
- Subscribe in `OnEnable`, unsubscribe in `OnDisable`.
- Never call `GameEvents.ClearAll()` during active gameplay — it is reserved for
  full-app teardown.
- For game-specific events that only one display cares about, use a `static event`
  on the manager (e.g., `TelephoneManager.RevealEntryChanged`) rather than adding
  fields to `GameEvents`.

---

## 4. Message Protocol

Every message type string **must** come from `MessageTypes` in
`Assets/Scripts/Network/MessageModels.cs`.

```csharp
// Good
case MessageTypes.SubmitAnswer:
// Bad
case "submit_answer":
```

When adding a new game, add its message constants to `MessageTypes` first,
then reference them in both the C# manager and the phone web-app JavaScript.

---

## 5. `JsonUtility` Caveats

- **Structs** deserialized with `JsonUtility.FromJson<T>()` are never `null`;
  they return a default-initialized value. Validate payload fields
  (e.g., `string.IsNullOrEmpty(msg.gameId)`) instead of checking `msg == null`.
- `JsonUtility` does **not** support `Dictionary`, polymorphism, or properties.
  Use flat `[Serializable]` classes with public fields.
- Keep DTOs in `MessageModels.cs` (or co-located with their game manager for
  game-specific types like `FibbagePrompt`).

---

## 6. GC Discipline

No per-frame heap allocations in:
- `Update` / `FixedUpdate`
- Broadcast paths (anything called every N physics frames)
- Input relay message handlers (called at 30-60 Hz per player)

Specific patterns to avoid:
- `int.ToString()` or `float.ToString()` — cache the last displayed value and only
  update the TMP text when it changes.
- `new List<T>()` + `.ToArray()` every broadcast tick — pre-allocate and reuse.
- `enum.ToString()` — cache a `string[]` or `Dictionary<TState, string>` mapping.
- `json.Substring(...)` in hot paths — use index-based parsing or pre-allocated DTOs.

Patterns that are fine:
- Allocations in `Start`, `Awake`, `OnSessionStart`, or once-per-round setup.
- `JsonUtility.ToJson` in round-transition broadcasts (a few times per minute).

---

## 7. LINQ Policy

**Banned** in `Update`, `FixedUpdate`, per-frame broadcasts, tally inner loops,
and input handlers. LINQ extension methods allocate enumerator objects and closures.

**Allowed** in lobby code, once-per-round setup, `OnSessionStart`, `OnSessionEnd`,
and editor-only paths.

When tempted to use `.Where(...).ToList()` in a hot path, write a `for` loop instead.

---

## 8. `FindObjectOfType` / `FindObjectsByType`

- Acceptable as a **one-shot** in `Awake` or `Start` to discover scene-authored
  objects (goal zones, spawners, registries).
- **Never** in `Update`, `FixedUpdate`, or any per-frame code.
- Prefer explicit `[SerializeField]` references in the Inspector when the object
  is known at edit time.

---

## 9. Physics Conventions

| Game | Plane | Physics | Components |
|------|-------|---------|------------|
| Pong | XY | 2D | `Rigidbody2D`, `Collider2D`, `PhysicsMaterial2D` |
| Sheep Herder | XY | 2D | `Rigidbody2D`, `Collider2D` |
| Future 3D games | XZ | 3D | `Rigidbody`, `Collider` |

Never mix 2D and 3D physics components on the same GameObject — Unity will throw
"Can't add component because it conflicts with the existing derived component."

---

## 10. Logging

`GameLog` methods should be guarded so they are stripped from release builds:
- Use `[System.Diagnostics.Conditional("UNITY_EDITOR")]` or
  `[System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]` on log methods.
- Avoid string interpolation in log calls that run every frame — the interpolation
  happens even if the log is stripped.

---

## 11. Adding a New Game — Checklist

1. **Define message types** in `MessageTypes` (`MessageModels.cs`).
2. **Create DTO classes** in `MessageModels.cs` (or a co-located file).
3. **Create a `GameRegistryEntry` asset** (right-click → Create → Party Game → Game Entry).
4. **Create a scene** (`Assets/Scenes/YourGame.unity`), add it to Build Settings.
5. **Implement your manager:**
   - Round-based (prompts, answers, votes, scoring) → extend `RoundBasedGameSession<TState>`.
   - Real-time (frame-driven, no rounds) → implement `IGameSession` directly.
6. In `Start()`, call `GameCoordinator.Instance.RegisterSession(this)`.
7. Use `MessageTypes.*` constants in `OnGameMessage` switch cases.
8. Fire `GameEvents.FireDisplayState(...)` on phase transitions so `GameTableDisplay` updates.
9. Add display-name and phase overrides to `GameTableDisplay` dictionaries.
10. End the game by calling `GameCoordinator.Instance.OnGameEnded()`.

---

## 12. Performance Budgets

| Metric | Target |
|--------|--------|
| Steady-state GC | < 1 KB/frame during active play |
| Full-state broadcast | < 50 ms including JSON serialization |
| Frame rate | 60 FPS with 8 players connected |
| Input latency | < 2 frames from WS receive to paddle/joystick update |

Profile with Unity Profiler's GC Alloc column and the Deep Profile mode when
investigating regressions.
