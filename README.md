# The Table 30A - Party Games

A local multiplayer party game system for venues, parties, and gatherings. One shared screen (TV/projector) runs the Unity app while players join and play from their phones via a web browser over local WiFi. Supports multiple game types — players vote on which game to play between rounds.

## How It Works

1. The Unity app runs on the main screen and acts as the game server
2. It starts a combined HTTP + WebSocket server on a single port and broadcasts its presence on the LAN
3. Players open a URL on their phones, see available games on the network, and tap to join
4. The first player to join becomes the **host** and gets a "Start Game" button on their phone
5. The host starts game selection — everyone votes on which game to play
6. The winning game loads, everyone plays, and then it's back to voting for the next game

## Available Games

### Quiplash
Players are given a funny prompt. Everyone writes an answer, then votes for the best one. Points are awarded based on votes. After several rounds, a winner is crowned.

### Pong Arena
An N-player Pong game where the arena is a polygon with one side per player. Each player controls a paddle from their phone by dragging left/right. The ball bounces inside the polygon — miss it and you lose a life. Last player standing wins.

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

### 5. Create the Pong Scene

1. **File → New Scene**, save as `Assets/Scenes/Pong.unity`
2. Create one **empty GameObject**:
   - **PongManager** → attach `PongManager.cs` (it auto-creates PongArena and PongBall children at runtime)
3. Set the **Main Camera** to Orthographic, size ~6, position (0, 0, -10)

### 6. Add Scenes to Build Settings

**File → Build Settings**, then add all three scenes:

| Scene | Build Index |
|-------|-------------|
| Lobby | 0 (must be first) |
| Quiplash | 1 |
| Pong | 2 |

### 7. Windows Firewall

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
4. Enter a name and you're in the lobby
5. The **first player** to join becomes the host (shown with a star)

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
| Start Lives | 3 | Lives each player starts with |
| Countdown Seconds | 3 | Countdown before the ball launches |
| Goal Pause Seconds | 1.5 | Pause after a goal before play resumes |
| Game Over Display Seconds | 8 | How long the winner screen shows |

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
    Games/
      Quiplash/
        QuiplashManager.cs       Quiplash game session (implements IGameSession)
      Pong/
        PongManager.cs           Pong game session (implements IGameSession)
        PongArena.cs             Polygon arena generation + rendering
        PongBall.cs              Ball movement + collision detection
        PongPhysics.cs           Geometry helpers (intersection, reflection)
    GameEvents.cs                Event bus (decouples network from logic)
    GameLog.cs                   Structured color-coded logging
  StreamingAssets/
    prompts.json                 Quiplash prompts
    WebApp/
      index.html                 Player web app (all screens)
      style.css                  Mobile-first styles
      app.js                     Core: connection, discovery, screen routing
      games/
        quiplash.js              Quiplash phone UI module
        pong.js                  Pong paddle controller module
  Scenes/
    Lobby.unity                  Main scene (persistent managers)
    Quiplash.unity               Quiplash game scene
    Pong.unity                   Pong game scene
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

### Pong arena looks wrong

- Make sure the Pong scene camera is **Orthographic**, size ~6, at position (0, 0, -10)
- The arena scales based on player count (3 players = triangle, 4 = square, etc.)

### No prompts loading

- Ensure `Assets/StreamingAssets/prompts.json` exists and is valid JSON
- Check the Unity Console for parse errors
- The game falls back to built-in prompts if the file is missing
