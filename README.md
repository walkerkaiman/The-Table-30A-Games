# The Table 30A - Party Games

A local multiplayer party game system designed for tabletop projection at venues, parties, and gatherings. A projector displays the Unity app onto a long table while players sit on both sides and join from their phones via a web browser over local WiFi. Supports multiple game types — players vote on which game to play between rounds.

## How It Works

1. The Unity app runs on a computer connected to a projector aimed down at a long table
2. It starts a combined HTTP + WebSocket server on a single port and broadcasts its presence on the LAN
3. Players open a URL on their phones, see available games on the network, and tap to join
4. Each player picks which **side of the table** they're sitting on ("This Side" or "That Side")
5. The first player to join becomes the **host** and gets a "Start Game" button on their phone
6. The host starts game selection — everyone votes on which game to play
7. The winning game loads, everyone plays, and then it's back to voting for the next game
8. The projected display is **mirrored** so both sides of the table can read text and see the game right-side-up

## Available Games

### Quiplash
Players are given a funny prompt. Everyone writes an answer, then votes for the best one. Points are awarded based on votes. After several rounds, a winner is crowned.

### Pong
An N-player Pong game. Each player controls a paddle from their phone by dragging left/right. The ball bounces off walls and paddles — miss it and you lose a life. Last player standing wins. You design the arena layout, collision boundaries, and all visuals in Unity; PongManager handles scoring, ball movement, and game state.

### Fibbage (Bluffing Game)
A trivia-meets-deception game where knowing the truth is only half the battle.

**How to Play:**

1. **The Prompt** — The table displays a statement with a blank (e.g., "In 2014, a Florida man was arrested for throwing ___ at his neighbor."). The real answer exists but is obscure enough that most people won't know it.

2. **Write a Bluff** — Each player submits a fake answer on their phone. The goal is to write something believable enough that other players think it's the truth. You have 45 seconds.

3. **Vote** — The game shuffles your bluffs together with the real answer and presents all options. Everyone votes for the answer they think is **actually true**. You cannot vote for your own bluff. You have 20 seconds.

4. **Scoring** — Points are awarded two ways:
   - **+1000 points** for correctly identifying the truth
   - **+500 points** for each player fooled by your bluff
   - **+250 bonus** if nobody finds the truth and your bluff got the most votes (master deceiver!)

5. **Reveal** — The real answer is shown, along with who fooled whom and how many points each player earned.

6. **Winner** — After all rounds (default 5), the player with the highest total score wins.

**Example Round:**
- Prompt: "The original name for Google was ___."
- Alice writes: "SearchWave" / Bob writes: "NetCrawler" / The truth is: "Backrub"
- Voting: Alice picks "NetCrawler" (Bob's bluff), Bob picks "Backrub" (the truth)
- Result: Bob gets +1000 (found truth) + 0 (nobody picked his bluff). Alice gets +0 (wrong pick) + 500 (fooled nobody... wait, nobody picked hers either). Bob's bluff fooled nobody, but Alice's bluff fooled nobody too. Bob wins the round with 1000 pts.

**Tips:** The best bluffs sound just plausible enough. Match the tone and specificity of how a real fact would sound.

### Speed Draw
A collaborative drawing game where everyone draws together in just 3 seconds.

1. The table shows a drawing prompt (e.g., "A cat wearing sunglasses")
2. Players get 10 seconds to plan
3. Everyone has exactly 3 seconds to draw on a shared canvas from their phones simultaneously
4. After drawing, 3 labels are shown (the real prompt + 2 decoys) and everyone guesses which was the actual prompt
5. Scoring: everyone who picks correctly earns team points, plus a bonus for players who actually contributed strokes

### Hot Potato
A fast-paced passing game. Players are arranged in a circle on the table.

1. One player starts with a virtual potato
2. The potato holder sees pass controls on their phone (swipe left, right, or throw across)
3. A hidden timer ticks down — when it hits zero, the potato EXPLODES
4. The player holding the potato when it explodes gets a strike
5. 3 strikes and you're out — last player standing wins

### Caption Contest
Like Quiplash but with images. A funny or unusual image is shown on the table, and players write the best caption.

1. The table displays an image from a local folder
2. Everyone writes a caption on their phone
3. All captions are revealed and players vote for the funniest
4. Points are awarded based on votes
5. Images are loaded from `StreamingAssets/GameContent/caption_contest/` and served via `StreamingAssets/WebApp/media/caption_contest/` — swap files to update content without code changes

### Price is Close
A guessing game where players estimate the price of items, products, or experiences.

1. The table shows an item with an image or video
2. Players submit a numeric guess on their phone
3. The real price is revealed with a dramatic ranking
4. Scoring: exact = 1000 pts, within 5% = 700, within 10% = 400, plus +200 bonus for closest guess
5. Items and media are loaded from `StreamingAssets/GameContent/price_is_close/` and `StreamingAssets/WebApp/media/price_is_close/`

## Prerequisites

- **Unity 2022.3** (LTS) or newer
- A **local WiFi network** that the host computer and all player phones are connected to
- **Windows 10/11** (the embedded server uses TCP sockets)

## Setup

### 1. Open the Project

Open this folder in Unity Hub. Unity will import the project and generate the necessary files.

### 2. Create ScriptableObject Assets

These define which games are available in the system:

1. In the Project window, right-click → **Create → Party Game → Game Registry** — name it `GameRegistry`
2. Right-click → **Create → Party Game → Game Entry** — name it `Quiplash`
   - Set: `id` = `quiplash`, `Display Name` = `Quiplash`, `Description` = `Write funny answers, vote for the best!`, `Scene Name` = `Quiplash`, `Min Players` = `3`, `Max Players` = `8`
3. Right-click → **Create → Party Game → Game Entry** — name it `Pong`
   - Set: `id` = `pong`, `Display Name` = `Pong Arena`, `Description` = `Control your paddle, last one standing wins!`, `Scene Name` = `Pong`, `Min Players` = `2`, `Max Players` = `8`
4. Open the `GameRegistry` asset and drag both entries into its `Entries` list

### 3. Set Up the Lobby Scene (Main Scene)

This is the scene Unity loads on startup. It contains the persistent managers:

1. Open or create your main scene (e.g., `Assets/Scenes/Lobby.unity`)
2. Create three **empty GameObjects** and attach one script to each:
   - **NetworkManager** → attach `NetworkManager.cs`
   - **PlayerManager** → attach `PlayerManager.cs`
   - **GameCoordinator** → attach `GameCoordinator.cs`
3. Select the **GameCoordinator** object and drag the `GameRegistry` asset into its `Game Registry` field in the Inspector

All three are singletons with `DontDestroyOnLoad` — they persist across scene transitions.

### 4. Create the Quiplash Scene

1. **File → New Scene**, save as `Assets/Scenes/Quiplash.unity`
2. Create three **empty GameObjects** and attach one script to each:
   - **QuiplashManager** → attach `QuiplashManager.cs`
   - **RoundManager** → attach `RoundManager.cs`
   - **PromptDatabase** → attach `PromptDatabase.cs`
3. Select the **Main Camera** and add the `MirroredTableCamera` component so both sides of the table can read prompts and results

### 5. Create the Pong Scene

1. **File → New Scene**, save as `Assets/Scenes/Pong.unity`
2. Set the **Main Camera** to Orthographic, size ~6, position (0, 0, -10)
3. Select the **Main Camera** and add the `MirroredTableCamera` component so both sides of the table see the game right-side-up
4. Create a **PongManager** GameObject and attach `PongManager.cs`
   - In the Inspector, assign your **Ball Prefab** (a prefab with a sprite/mesh for the ball — add a `Rigidbody2D` + `CircleCollider2D` if you want Unity physics bouncing; otherwise PongManager moves the ball's transform directly)
   - Configure ball speed, speed increment, and max speed
5. As a child of PongManager, create a **PlayerInputRelay** GameObject and attach `PlayerInputRelay.cs`
   - Assign your **Player Node Prefab** (the paddle visual) to the relay's `Player Node Prefab` field
   - Set `Position Axis`, `Position Range`, `Position Offset`, and `Side Offset` to match your arena layout
6. Build your arena visuals and collision boundaries:
   - Add walls/borders as GameObjects with `BoxCollider2D` (or `EdgeCollider2D`) so the ball bounces off them
   - Create a **Physics Material 2D** with `Bounciness = 1` and `Friction = 0`, assign it to both the ball's collider and the wall colliders for perfect bouncing
7. Add **goal zones** (one per table side):
   - Create a GameObject (e.g. `GoalZone_ThisSide`) behind where that side's paddles sit
   - Add a `BoxCollider2D`, check **Is Trigger**, and size it to cover the goal opening
   - Attach `PongGoalZone.cs` and set `Table Side` in the Inspector: **0** for "This Side", **1** for "That Side"
   - PongManager automatically assigns the correct player to each goal zone at runtime based on table side — no manual player ID configuration needed

Player prefabs spawned by the `PlayerInputRelay` are automatically offset to the correct table half via `sideOffset`: "This Side" players appear at `-sideOffset`, "That Side" players at `+sideOffset`.

**Ball requirements for triggers:** The ball prefab must have a `Rigidbody2D` (gravity scale 0) and a `Collider2D` (e.g. `CircleCollider2D`). Without a Rigidbody2D on at least one object, Unity won't fire `OnTriggerEnter2D`.

### 6. Create the Fibbage Scene

1. **File → New Scene**, save as `Assets/Scenes/Fibbage.unity`
2. Create a **FibbageManager** GameObject and attach `FibbageManager.cs`
3. Select the **Main Camera** and add the `MirroredTableCamera` component
4. Prompts are loaded from `Assets/StreamingAssets/GameContent/fibbage/prompts.json`

### 7. Create the Speed Draw Scene

1. **File → New Scene**, save as `Assets/Scenes/SpeedDraw.unity`
2. Create a **SpeedDrawManager** GameObject and attach `SpeedDrawManager.cs`
3. Select the **Main Camera** and add the `MirroredTableCamera` component
4. Prompts are loaded from `Assets/StreamingAssets/GameContent/speed_draw/prompts.json`

### 8. Create the Hot Potato Scene

1. **File → New Scene**, save as `Assets/Scenes/HotPotato.unity`
2. Create a **HotPotatoManager** GameObject and attach `HotPotatoManager.cs`
3. Select the **Main Camera** and add the `MirroredTableCamera` component

### 9. Create the Caption Contest Scene

1. **File → New Scene**, save as `Assets/Scenes/CaptionContest.unity`
2. Create a **CaptionContestManager** GameObject and attach `CaptionContestManager.cs`
3. Select the **Main Camera** and add the `MirroredTableCamera` component
4. Items are loaded from `Assets/StreamingAssets/GameContent/caption_contest/items.json`
5. Images must be placed in `Assets/StreamingAssets/WebApp/media/caption_contest/` and referenced by URL in the JSON (e.g. `/media/caption_contest/photo.jpg`)

### 10. Create the Price is Close Scene

1. **File → New Scene**, save as `Assets/Scenes/PriceIsClose.unity`
2. Create a **PriceIsCloseManager** GameObject and attach `PriceIsCloseManager.cs`
3. Select the **Main Camera** and add the `MirroredTableCamera` component
4. Items are loaded from `Assets/StreamingAssets/GameContent/price_is_close/items.json`
5. Images/videos must be placed in `Assets/StreamingAssets/WebApp/media/price_is_close/` and referenced by URL in the JSON

### 11. Create Game Registry Entries

For each new game, create a **Game Entry** asset via `Create → Party Game → Game Entry`:

| Game | id | Display Name | Scene Name | Min Players | Max Players |
|------|----|-------------|------------|-------------|-------------|
| Fibbage | `fibbage` | Fibbage | Fibbage | 3 | 8 |
| Speed Draw | `speed_draw` | Speed Draw | SpeedDraw | 2 | 8 |
| Hot Potato | `hot_potato` | Hot Potato | HotPotato | 3 | 10 |
| Caption Contest | `caption_contest` | Caption Contest | CaptionContest | 3 | 8 |
| Price is Close | `price_is_close` | Price is Close | PriceIsClose | 2 | 10 |

Drag all new entries into the `GameRegistry` asset's `Entries` list.

### 12. Add Scenes to Build Settings

**File → Build Settings**, then add all scenes:

| Scene | Build Index |
|-------|-------------|
| Lobby | 0 (must be first) |
| Quiplash | 1 |
| Pong | 2 |
| Fibbage | 3 |
| SpeedDraw | 4 |
| HotPotato | 5 |
| CaptionContest | 6 |
| PriceIsClose | 7 |

### 13. Windows Firewall

The first time you run, Windows may prompt you to allow Unity through the firewall. **Allow it** on private networks. If you missed the prompt:

1. Open **Windows Defender Firewall**
2. Click **Allow an app through firewall**
3. Find **Unity Editor** (or your built .exe) and enable it for **Private** networks

## Running the Game

### Start the Host

1. Press **Play** in the Unity Editor
2. The Console will show:

```
════════════════════════════════════════
  ROOM CODE:  ABCD
  JOIN URL:   http://192.168.1.100:7777
  GAMES:      2 registered
════════════════════════════════════════
```

### Players Join

1. Make sure phones are on the **same WiFi network**
2. Open a browser on each phone and go to the URL shown in the console
3. The phone will show available games on the network — tap the game to join
4. Enter a name
5. Choose which **side of the table** you're sitting on ("This Side" or "That Side")
6. You're in the lobby — the **first player** to join becomes the host (shown with a star)

### Play

1. The **host** taps **Start Game** on their phone (or press Space on the keyboard as a fallback)
2. **Game Selection** — all players vote on which game to play. The host can tap **Skip Vote** to force-advance.
3. The winning game loads and plays
4. When the game ends, it returns to game selection for the next round
5. Players stay connected the entire time — no need to rejoin

## Configuration

### GameCoordinator (on the Lobby scene)

| Setting | Default | Description |
|---------|---------|-------------|
| Game Registry | — | Drag the GameRegistry asset here |
| Min Players To Start | 2 | Minimum players before the host can start |
| Vote Timer Seconds | 15 | How long players have to vote on a game |

### NetworkManager

| Setting | Default | Description |
|---------|---------|-------------|
| Port | 7777 | Port for HTTP + WebSocket |
| Game Name | "Game 1" | Name shown in LAN discovery |

### QuiplashManager (on the Quiplash scene)

| Setting | Default | Description |
|---------|---------|-------------|
| Max Rounds | 5 | Rounds per game |
| Prompt Display Time | 5s | How long the prompt shows before answers open |
| Answer Timer Seconds | 60s | Time limit for submitting answers |
| Vote Timer Seconds | 30s | Time limit for voting |
| Results Display Time | 10s | How long results show before next round |
| Game Over Display Time | 12s | How long the final standings display |
| Points Per Vote | 100 | Points per vote received |

### PongManager (on the Pong scene)

| Setting | Default | Description |
|---------|---------|-------------|
| Ball Prefab | — | Your ball prefab (sprite/mesh). Add Rigidbody2D + Collider2D for physics bouncing, or leave physics-free for manual movement. |
| Ball Speed | 5 | Initial ball speed |
| Ball Speed Increment | 0.3 | Speed increase after each goal |
| Ball Max Speed | 15 | Maximum ball speed |
| Start Lives | 3 | Lives each player starts with |
| Countdown Seconds | 3 | Countdown before the ball launches |
| Goal Pause Seconds | 1.5 | Pause after a goal before play resumes |
| Game Over Display Seconds | 8 | How long the winner screen shows |
| Broadcast Every N Frames | 3 | Send a pong_frame update every N FixedUpdate frames |

### FibbageManager (on the Fibbage scene)

| Setting | Default | Description |
|---------|---------|-------------|
| Max Rounds | 5 | Rounds per game |
| Prompt Display Time | 5s | How long the prompt shows before bluffing opens |
| Bluff Timer Seconds | 45s | Time to write a fake answer |
| Vote Timer Seconds | 20s | Time to pick the truth |
| Results Display Time | 8s | How long results show |
| Truth Points | 1000 | Points for picking the real answer |
| Fooled Points | 500 | Points per player fooled by your bluff |
| Best Bluff Bonus | 250 | Bonus if nobody found truth and your bluff won |

### SpeedDrawManager (on the SpeedDraw scene)

| Setting | Default | Description |
|---------|---------|-------------|
| Max Rounds | 5 | Rounds per game |
| Plan Seconds | 10 | Thinking time before drawing |
| Draw Seconds | 3 | Drawing time |
| Guess Seconds | 15 | Time to guess which label matches |
| All Correct Points | 300 | Points if everyone guesses right |
| Majority Correct Points | 200 | Points if majority guesses right |
| Contribution Bonus | 50 | Bonus for players who drew enough strokes |

### HotPotatoManager (on the HotPotato scene)

| Setting | Default | Description |
|---------|---------|-------------|
| Max Strikes | 3 | Strikes before elimination |
| Min Round Time | 5 | Minimum fuse time (seconds) |
| Max Round Time | 15 | Maximum fuse time (seconds) |
| Pass Cooldown | 0.3 | Minimum time between passes (seconds) |

### CaptionContestManager (on the CaptionContest scene)

| Setting | Default | Description |
|---------|---------|-------------|
| Max Rounds | 5 | Rounds per game |
| Image Display Time | 5s | How long image shows before captions open |
| Caption Timer Seconds | 45s | Time to write a caption |
| Vote Timer Seconds | 20s | Time to vote on captions |
| Points Per Vote | 100 | Points per vote received |

### PriceIsCloseManager (on the PriceIsClose scene)

| Setting | Default | Description |
|---------|---------|-------------|
| Max Rounds | 5 | Rounds per game |
| Item Display Time | 5s | How long the item shows before guessing opens |
| Guess Timer Seconds | 30s | Time to submit a numeric guess |
| Exact Points | 1000 | Points for exact guess |
| Within 5 Points | 700 | Points if within 5% of correct |
| Within 10 Points | 400 | Points if within 10% of correct |
| Closest Bonus Points | 200 | Bonus for the closest guess |

### MirroredTableCamera (on the Main Camera in each game scene)

| Setting | Default | Description |
|---------|---------|-------------|
| Mirror Enabled | true | Toggle the mirrored display on/off |
| Split Gap | 0.005 | Gap between the two halves in viewport units (0-0.05). Adds a thin divider line. |

Attach this to the Main Camera in any scene projected onto the table. The bottom half renders normally for near-side players. The top half is rotated 180 degrees so far-side players can read it.

### PlayerInputRelay (on any game scene with real-time phone input)

| Setting | Default | Description |
|---------|---------|-------------|
| Player Node Prefab | — | Optional prefab spawned per player (for custom visuals). If empty, creates bare GameObjects. |
| Listen Message Types | `paddle_move`, `player_input` | Which phone message types this relay intercepts |
| Input Field Name | `position` | JSON field to extract as the primary 0-1 input. Falls back to `x` if not found. |
| Apply To Transform | true | Move each node's transform based on input |
| Position Axis | (1, 0, 0) | World axis the 0-1 input maps onto |
| Position Range | 8 | Total world-space extent (centered on offset). 0 maps to -4, 1 maps to +4. |
| Position Offset | (0, 0, 0) | World-space origin for position mapping |
| Smooth Speed | 20 | Lerp speed for transform movement. 0 = instant snap. |
| Side Offset | (0, 3, 0) | Displacement from center per table side. "This Side" (0) gets -offset, "That Side" (1) gets +offset. For a table split along Y, use (0, N, 0). |

## Prompts

Prompts are stored in `Assets/StreamingAssets/prompts.json`:

```json
{
  "prompts": [
    {
      "id": "prompt_001",
      "text": "The worst thing to say at a job interview",
      "category": "workplace"
    }
  ]
}
```

A Cursor rule at `.cursor/rules/generate-prompts.mdc` teaches the AI how to generate new prompts. Open Cursor chat and ask:
- *"Generate 50 new game prompts"*
- *"Add 20 more prompts in the social category"*

## Game Content (Local Files)

All game-specific content lives under `Assets/StreamingAssets/GameContent/{gameId}/`. Media that phones need to display goes under `Assets/StreamingAssets/WebApp/media/{gameId}/`.

### Fibbage Prompts

File: `Assets/StreamingAssets/GameContent/fibbage/prompts.json`

```json
{
  "prompts": [
    {
      "id": "fib_001",
      "text": "In 2014, a Florida man was arrested for throwing ___ at his neighbor.",
      "truth": "a live alligator",
      "category": "florida"
    }
  ]
}
```

### Speed Draw Prompts

File: `Assets/StreamingAssets/GameContent/speed_draw/prompts.json`

```json
{
  "prompts": [
    {
      "id": "sd_001",
      "text": "A cat wearing sunglasses",
      "decoyA": "A dog in a top hat",
      "decoyB": "A bird with headphones"
    }
  ]
}
```

### Caption Contest Items

JSON index: `Assets/StreamingAssets/GameContent/caption_contest/items.json`
Phone-visible media: `Assets/StreamingAssets/WebApp/media/caption_contest/`

```json
{
  "items": [
    {
      "id": "cc_001",
      "imageUrl": "/media/caption_contest/funny_photo.jpg",
      "category": "funny"
    }
  ]
}
```

To add new images: drop them into the `WebApp/media/caption_contest/` folder and add an entry to `items.json`. No code changes needed.

### Price is Close Items

JSON index: `Assets/StreamingAssets/GameContent/price_is_close/items.json`
Phone-visible media: `Assets/StreamingAssets/WebApp/media/price_is_close/`

```json
{
  "items": [
    {
      "id": "pic_001",
      "title": "Vintage Typewriter",
      "description": "A fully restored 1960s Olivetti typewriter.",
      "correctPrice": 350,
      "unit": "$",
      "imageUrl": "/media/price_is_close/typewriter.jpg",
      "videoUrl": ""
    }
  ]
}
```

To add new items: drop images/videos into `WebApp/media/price_is_close/`, add an entry to `items.json` with the correct price and media path. Supported media types: `.jpg`, `.png`, `.gif`, `.mp4`, `.webm`.

## Project Structure

```
Assets/
  Scripts/
    Network/
      GameServer.cs              Combined HTTP + WebSocket server
      WebSocketConnection.cs     Per-client WebSocket wrapper
      NetworkManager.cs          Transport layer, message routing
      GameDiscovery.cs           UDP broadcast for LAN discovery
      MessageModels.cs           JSON message data classes
    Game/
      GameCoordinator.cs         Top-level state machine (Lobby/GameSelect/InGame)
      PlayerManager.cs           Player registry, scores, host tracking
      IGameSession.cs            Interface all games implement
      GameRegistry.cs            ScriptableObject catalog of games
      GameRegistryEntry.cs       ScriptableObject per game type
      PromptDatabase.cs          Quiplash prompt loader
    Input/
      PlayerInputRelay.cs        Spawns per-player child objects, receives real-time phone input
      PlayerInputNode.cs         Per-player data component (raw input, identity, color, table side)
    Display/
      MirroredTableCamera.cs     Dual-camera mirrored display for tabletop projection
      QRCodeDisplay.cs           Generates WiFi + URL QR codes on Canvas
      LobbyDisplay.cs            Hides lobby UI during games
    Utilities/
      QRCodeEncoder.cs           Pure C# QR code generator
      GameContentLoader.cs       Reusable JSON content loader for game-specific data
    Games/
      Quiplash/
        QuiplashManager.cs       Quiplash game session
      Pong/
        PongManager.cs           Pong game session
        PongGoalZone.cs          Trigger-based goal detection
      Fibbage/
        FibbageManager.cs        Fibbage bluffing game session
      SpeedDraw/
        SpeedDrawManager.cs      Speed Draw collaborative drawing game session
      HotPotato/
        HotPotatoManager.cs      Hot Potato passing game session
      CaptionContest/
        CaptionContestManager.cs Caption Contest image-caption game session
      PriceIsClose/
        PriceIsCloseManager.cs   Price is Close guessing game session
    GameEvents.cs                Event bus (decouples network from logic)
    GameLog.cs                   Structured color-coded logging
  StreamingAssets/
    prompts.json                 Quiplash prompts
    GameContent/
      fibbage/prompts.json       Fibbage prompts with truth answers
      speed_draw/prompts.json    Speed Draw prompts with decoys
      caption_contest/items.json Caption Contest image index
      price_is_close/items.json  Price is Close item index
    WebApp/
      index.html                 Player web app (all screens)
      style.css                  Mobile-first styles
      app.js                     Core: connection, discovery, screen routing
      games/
        quiplash.js              Quiplash phone UI module
        pong.js                  Pong paddle controller module
        fibbage.js               Fibbage bluffing phone UI module
        speed_draw.js            Speed Draw drawing + guessing module
        hot_potato.js            Hot Potato passing + swipe module
        caption_contest.js       Caption Contest phone UI module
        price_is_close.js        Price is Close guessing module
      media/
        caption_contest/         Local images for Caption Contest (add .jpg/.png here)
        price_is_close/          Local images/videos for Price is Close (add .jpg/.mp4 here)
  Scenes/
    Lobby.unity                  Main scene (persistent managers)
    Quiplash.unity               Quiplash game scene
    Pong.unity                   Pong game scene
    Fibbage.unity                Fibbage game scene
    SpeedDraw.unity              Speed Draw game scene
    HotPotato.unity              Hot Potato game scene
    CaptionContest.unity         Caption Contest game scene
    PriceIsClose.unity           Price is Close game scene
```

## Architecture

### Overview

The system runs entirely on local WiFi with no cloud services:

- **Single port** (default 7777) handles both HTTP file serving and WebSocket connections
- **LAN Discovery**: the server broadcasts via UDP so phones can find games without entering IPs
- **GameCoordinator** orchestrates the top-level flow: Lobby → GameSelect → InGame → GameSelect → ...
- **IGameSession** interface lets each game type plug in with a standardized contract
- **GameRegistry** (ScriptableObject) catalogs available games — adding a new game is just adding an entry and a scene
- **Modular web app**: `app.js` is the core (connection, discovery, screen routing); game-specific UI lives in `games/*.js` modules activated by the server's `gameType` field

### Message Flow

```
Phone → WebSocket → NetworkManager → GameEvents → GameCoordinator → IGameSession
IGameSession → GameEvents.FireBroadcast → NetworkManager → WebSocket → Phone
```

### Table Sides

The system is designed for a projector aimed down at a long table with players sitting on both sides. During registration, each player picks **"This Side"** (0) or **"That Side"** (1). This information flows through the entire system:

- **PlayerManager** stores `tableSide` per player.
- **PlayerInfo** messages include `tableSide`, so the phone UI shows a colored indicator for each player's side.
- **PlayerInputRelay** automatically offsets spawned prefabs to the correct physical half of the table via the `sideOffset` field. "This Side" nodes are placed at `-sideOffset`, "That Side" nodes at `+sideOffset`. This is game-agnostic — no game needs to implement its own side logic.
- **PlayerInputNode** exposes `TableSide` so any custom logic can still read a player's side if needed.

### Mirrored Table Display

The **MirroredTableCamera** component splits the main camera into two halves:

- **Bottom half**: renders normally, readable for "This Side" players (tableSide 0).
- **Top half**: renders rotated 180 degrees, readable for "That Side" players (tableSide 1).

This ensures text (Quiplash prompts/results) and spatial games (Pong arena) are visible from both sides. Attach it to the Main Camera in each game scene. Works with all world-space rendering (LineRenderers, TextMeshes, sprites, game objects). A configurable gap provides a thin visual divider between the halves.

### Player Input Relay

For games that need near-realtime phone input (e.g., paddle positions in Pong), the **PlayerInputRelay** system decouples network messages from game logic:

- A `PlayerInputRelay` component in the game scene subscribes directly to `GameEvents.GameMessageReceived` for minimal-latency input.
- On session start, it spawns one child `PlayerInputNode` GameObject per player (optionally from a prefab for custom visuals).
- Each node stores the player's raw 0-1 input value, table side, and optionally maps it to a world-space transform position.
- Game logic reads from the relay (`GetRawInput(id)`, `GetNode(id)`, or child transforms) instead of managing its own input dictionaries.
- For Pong, the relay intercepts `paddle_move` messages and moves paddle prefabs automatically. PongManager reads paddle positions from the relay for broadcasting.

This lets you control player visual representation (sprites, models, effects) via prefabs and Inspector settings, independent of game logic.

### Host Player

The first player to join is the host. They get:
- A **Start Game** button in the lobby
- A **Skip Vote** button during game selection
- A star badge next to their name

If the host disconnects from the lobby, the next player is automatically promoted.

### Adding a New Game

1. Create a new scene with a MonoBehaviour that implements `IGameSession`
2. In `Start()`, call `GameCoordinator.Instance.RegisterSession(this)`
3. Handle messages in `OnGameMessage()`, broadcast state via `GameEvents.FireBroadcast()`
4. Call `GameCoordinator.Instance.OnGameEnded()` when the game finishes
5. Create a `GameRegistryEntry` ScriptableObject and add it to the `GameRegistry`
6. Create a `games/yourgame.js` module and register it with `GameApp.registerGameModule()`
7. Add the scene to Build Settings
8. *(If the game needs real-time phone input)* Add a `PlayerInputRelay` to the scene, configure its message types and position mapping, and call `relay.Initialize(playerIds)` in `OnSessionStart`. Read input via `relay.GetRawInput(id)` or use the spawned child transforms directly.

## Troubleshooting

### Players can't find the game

- Verify all devices are on the **same WiFi network**
- Check the **IP address** with `ipconfig` in Command Prompt
- Make sure **Windows Firewall** allows Unity on private networks
- Try disabling VPN if one is active
- Some public/corporate WiFi blocks device-to-device connections (client isolation)

### Players can't join (room code error)

- Room codes are generated automatically each session
- The phone discovers games via the network — the room code is sent automatically when you tap a game
- If joining manually, the code is 4 uppercase letters, case-insensitive

### WebSocket connection fails

- Check that port 7777 isn't used by another application
- Try a different port in the `NetworkManager` Inspector field

### Pong ball doesn't bounce

- If using Unity physics: ensure the ball prefab has a `Rigidbody2D` (gravity scale 0) and a `CircleCollider2D`, and that your walls have colliders
- If using manual movement: PongManager moves the ball's transform in `FixedUpdate` — add wall bouncing via your own trigger/collision scripts
- Make sure the Pong scene camera is **Orthographic**, size ~6, at position (0, 0, -10)

### Pong goals aren't triggering

- Each goal zone needs a 2D trigger collider and a script that calls `PongManager.Instance.ScoreGoal(playerId)`
- Make sure the ball has a `Rigidbody2D` (even if kinematic) and a collider for `OnTriggerEnter2D` to fire

### No prompts loading

- Ensure `Assets/StreamingAssets/prompts.json` exists and is valid JSON
- Check the Unity Console for parse errors
- The game falls back to built-in prompts if the file is missing
