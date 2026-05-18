# Treasure Hunter — Scene Wiring Guide

Open `Treasure Hunter.unity` in Unity and press Play.
The game is built to run out-of-the-box with **zero additional wiring** — every tile,
interactable, and camera is generated or positioned at runtime if the inspector
fields are left empty. Hook up your own art later by overriding any of the
fallbacks described below.

## Runtime fallbacks (new)

`DungeonPainter` auto-generates placeholder art for anything that is missing:

- Empty `floorTile` / `wallTile` / `spawnChamberTile` / `exitAreaTile` → procedural
  32-px solid-colour tiles are built in `ProceduralDungeonAssets`. Wall tiles
  get a `Tile.ColliderType.Grid` collider so explorers can't walk through walls.
- Empty `trapPrefabs`, `goldPickupPrefabs`, `pressurePlatePuzzlePrefab`, or
  `exitDoorPrefab` → procedural sprite-based GameObjects are instantiated at the
  generator's anchors. Each icon is visually distinct (red X trap, yellow coin,
  blue rune plate, door). These become the visible targets that the private
  clues in the briefing refer to.
- Missing Main-Camera framing → `DungeonPainter` centres the camera on the map
  and sizes the orthographic view to fit (toggle `autoFrameCamera` to disable).
- Missing `floorTilemap` / `wallTilemap` references → they are created under the
  painter's transform with a `Grid` parent at runtime.
- `FogOfWar` has no mesh / material → a built-in Quad + runtime material are
  auto-attached.

## Art override points (optional)

Wire any of these in the inspector to replace the procedural placeholder:

1. `DungeonPainter.floorTile` / `.wallTile` / `.spawnChamberTile` / `.exitAreaTile`
2. `DungeonPainter.trapPrefabs[]`         — `Trap` component prefabs
3. `DungeonPainter.goldPickupPrefabs[]`   — `GoldPickup` component prefabs
4. `DungeonPainter.pressurePlatePuzzlePrefab` — `PressurePlatePuzzle` with child `PressurePlate`s
5. `DungeonPainter.exitDoorPrefab`        — `ExitDoor` component prefab
6. `DungeonPainter.torchPropPrefabs[]`    — purely decorative
7. `FogOfWar.fogMaterial` — assign a material that uses `TreasureHunter/FogOfWar.shader`
   to guarantee the shader is included in player builds

## Gameplay clock (new)

Treasure Hunter uses a **count-up run clock** rather than a countdown:

- `briefingSeconds` / `deployCountdownSeconds` / `resultsHoldSeconds` still
  count down normally (short UI timers).
- Exploring + Escape share a single elapsed-time clock that ticks up to infinity.
  The phone HUD and shared screen display it as `MM:SS`.
- There is **no hard escape deadline**. Players finish when they walk through
  the exit, or when everyone has escaped. The escape bonus in the score
  formula decays linearly with elapsed seconds — fast runs still score higher.
- Score formula: `gold + puzzlesSolved*200 + (escaped? 500+escapeBonus : 0) − trapsTripped*50`,
  with `escapeBonus = max(0, escapeBonusMax − elapsed*escapeBonusDecayPerSec)`.

## Fog of war (new — simple torch model)

The fog is now purely a torch effect:

- Each non-escaped explorer has a torch with radius `Explorer.visionRadius` and
  a soft edge controlled by `FogOfWar.softEdgeWidth`.
- There is **no persistent "seen" memory**. As soon as a player moves away from
  an area it goes dark again. Splitting up expands total team vision; players
  glance at each other's positions on the shared table to coordinate.
- The shader is `Assets/Shaders/FogOfWar.shader`, material is created at
  runtime from it. Assign a material in the inspector if you want to tune
  `_FogColor` and `_SoftEdge` without touching code.

## Required GameObjects

### TreasureHunterManager (already in scene)

- Component: `TreasureHunterManager`. Inspector fields:
  - `briefingSeconds` (default 30)
  - `deployCountdownSeconds` (default 3)
  - `resultsHoldSeconds` (default 8)
  - `escapeBonusMax` / `escapeBonusDecayPerSec` (scoring tuning)
  - `defaultDifficulty` — expand and tune the `DifficultyProfile` fields
  - `broadcastEveryNFrames` (default 6)

### JoystickInputRelay (child of TreasureHunterManager)

- Set `playerNodePrefab` to `ExplorerPrefab.prefab`.
- Set `spawnPlane` to `XY`.

### ExplorerPrefab.prefab

At `Assets/Prefabs/TreasureHunter/ExplorerPrefab.prefab`, components:

- `PlayerJoystickNode`, `AcceleratedMover` (InputPlane=XY, maxSpeed=4)
- `Rigidbody2D` (Gravity=0, Freeze Z rotation)
- `CircleCollider2D`
- `Explorer`, `DownedController`, `DirectionalSpriteSwapper` (for directional sprites)

### DungeonPainter

- Add a GameObject with the `DungeonPainter` component. Nothing else is required;
  the painter auto-creates the `Grid` + `Tilemap`s and auto-frames the camera.
  Wire up authored tiles/prefabs when you have them.

### FogOfWar

- Add a GameObject with `MeshFilter` + `MeshRenderer` + `FogOfWar` script.
- Leave the MeshFilter's Mesh and the MeshRenderer's Material empty — the script
  installs Unity's Quad mesh and compiles the shader material on `Awake`.

### Camera

- The scene already has a Main Camera (orthographic). `DungeonPainter` moves and
  resizes it after painting unless `autoFrameCamera` is unchecked.

### GameTableDisplay (optional)

- Drop in a `GameTableDisplay` GameObject to show phase / timer / game name on
  the mirrored table. The display automatically switches to count-up mode
  when Treasure Hunter fires `DisplayTimerCountUpChanged(true)`.
