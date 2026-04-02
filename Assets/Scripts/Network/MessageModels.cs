using System;

// ──────────────────────────────────────────────
//  Base
// ──────────────────────────────────────────────

[Serializable]
public class BaseMessage
{
    public string type;
}

// ──────────────────────────────────────────────
//  Client → Server (common)
// ──────────────────────────────────────────────

[Serializable]
public class JoinMessage
{
    public string type;
    public string name;
    public string roomCode;
}

[Serializable]
public class RejoinMessage
{
    public string type;
    public string playerId;
    public string name;
}

// ──────────────────────────────────────────────
//  Client → Server (game select)
// ──────────────────────────────────────────────

[Serializable]
public class GameVoteMessage
{
    public string type;
    public string gameId;
}

// ──────────────────────────────────────────────
//  Client → Server (Quiplash)
// ──────────────────────────────────────────────

[Serializable]
public class SubmitAnswerMessage
{
    public string type;
    public string answer;
}

[Serializable]
public class VoteMessage
{
    public string type;
    public string answerId;
}

// ──────────────────────────────────────────────
//  Client → Server (Pong)
// ──────────────────────────────────────────────

[Serializable]
public class PaddleMoveMessage
{
    public string type;
    public float position;
}

// ──────────────────────────────────────────────
//  Server → Client (common)
// ──────────────────────────────────────────────

[Serializable]
public class WelcomeMessage
{
    public string type = "welcome";
    public string playerId;
    public string roomCode;
}

[Serializable]
public class RejoinSuccessMessage
{
    public string type = "rejoin_success";
    public string playerId;
}

[Serializable]
public class ErrorMessage
{
    public string type = "error";
    public string message;
}

[Serializable]
public class PlayerInfo
{
    public string id;
    public string name;
    public int score;
}

[Serializable]
public class PlayerListMessage
{
    public string type = "player_list";
    public PlayerInfo[] players;
    public string hostId;
}

[Serializable]
public class ConfirmationMessage
{
    public string type;
}

// ──────────────────────────────────────────────
//  Server → Client (game state — universal envelope)
// ──────────────────────────────────────────────

[Serializable]
public class GameStateMessage
{
    public string type = "game_state";
    public string gameType;
    public string state;
    public int timer;
    public PlayerInfo[] players;

    // Game select
    public GameSelectInfo[] games;
    public VoteCount[] voteCounts;

    // Quiplash
    public string prompt;
    public int round;
    public int totalRounds;
    public AnswerInfo[] answers;
    public ResultInfo[] results;
}

[Serializable]
public class GameSelectInfo
{
    public string id;
    public string name;
    public string description;
    public int minPlayers;
    public int maxPlayers;
}

[Serializable]
public class VoteCount
{
    public string gameId;
    public int count;
}

// ──────────────────────────────────────────────
//  Server → Client (game select vote tally)
// ──────────────────────────────────────────────

[Serializable]
public class VoteUpdateMessage
{
    public string type = "vote_update";
    public VoteCount[] votes;
}

// ──────────────────────────────────────────────
//  Quiplash data types
// ──────────────────────────────────────────────

[Serializable]
public class AnswerInfo
{
    public string id;
    public string text;
}

[Serializable]
public class ResultInfo
{
    public string id;
    public string name;
    public string answer;
    public int votes;
    public int score;
}

// ──────────────────────────────────────────────
//  Pong (server → client)
// ──────────────────────────────────────────────

[Serializable]
public class PongFrameMessage
{
    public string type = "pong_frame";
    public float bx;
    public float by;
    public PongPaddleState[] paddles;
}

[Serializable]
public class PongPaddleState
{
    public string id;
    public float position;
    public int side;
    public int lives;
}

[Serializable]
public class PongEventMessage
{
    public string type = "pong_event";
    public string eventName;
    public string playerId;
    public string scoredOn;
    public int livesLeft;
}

// ──────────────────────────────────────────────
//  LAN Discovery
// ──────────────────────────────────────────────

[Serializable]
public class DiscoveredGameInfo
{
    public string gameName;
    public string ip;
    public int port;
    public string roomCode;
    public int playerCount;
    public string state;
}

[Serializable]
public class DiscoveredGamesWrapper
{
    public DiscoveredGameInfo[] games;
}

[Serializable]
public class DiscoveryBroadcast
{
    public string serverId;
    public string gameName;
    public string ip;
    public int port;
    public string roomCode;
    public int playerCount;
    public string state;
}
