using System;

// ──────────────────────────────────────────────
//  Message type constants
// ──────────────────────────────────────────────

public static class MessageTypes
{
    public const string GameState = "game_state";
    public const string Join = "join";
    public const string Rejoin = "rejoin";
    public const string Welcome = "welcome";
    public const string RejoinSuccess = "rejoin_success";
    public const string Error = "error";
    public const string PlayerList = "player_list";
    public const string VoteUpdate = "vote_update";
    public const string StartGame = "start_game";
    public const string GameVote = "game_vote";
    public const string OpenRegistration = "open_registration";

    public const string SubmitAnswer = "submit_answer";
    public const string Vote = "vote";
    public const string AnswerReceived = "answer_received";
    public const string VoteReceived = "vote_received";

    public const string PaddleMove = "paddle_move";
    public const string PlayerInput = "player_input";
    public const string PongFrame = "pong_frame";
    public const string PongEvent = "pong_event";

    public const string SubmitBluff = "submit_bluff";
    public const string FibbageVote = "fibbage_vote";
    public const string BluffReceived = "bluff_received";
    public const string FibbageVoteReceived = "fibbage_vote_received";

    public const string DrawStroke = "draw_stroke";
    public const string DrawGuess = "draw_guess";
    public const string GuessReceived = "guess_received";

    public const string PotatoPass = "potato_pass";
    public const string PotatoEvent = "potato_event";

    public const string SubmitCaption = "submit_caption";
    public const string CaptionVote = "caption_vote";
    public const string CaptionReceived = "caption_received";
    public const string CaptionVoteReceived = "caption_vote_received";

    public const string SubmitGuess = "submit_guess";
}

// ──────────────────────────────────────────────
//  Base message for deserialization routing
// ──────────────────────────────────────────────

[Serializable]
public class BaseMessage
{
    public string type;
}

// ──────────────────────────────────────────────
//  Shared header fields for round-based game state messages
// ──────────────────────────────────────────────

[Serializable]
public class GameStateHeader
{
    public string type = MessageTypes.GameState;
    public string gameType;
    public string state;
    public int timer;
    public int round;
    public int totalRounds;
    public PlayerInfo[] players;
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
    public int tableSide;  // 0 = near side, 1 = far side
    public string playerId; // non-empty when an existing player is re-registering
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

[Serializable]
public class PlayerInputMessage
{
    public string type;   // "player_input"
    public float x;       // primary axis (0-1)
    public float y;       // secondary axis (0-1), optional
}

// ──────────────────────────────────────────────
//  Server → Client (common)
// ──────────────────────────────────────────────

[Serializable]
public class WelcomeMessage
{
    public string type = MessageTypes.Welcome;
    public string playerId;
    public string roomCode;
}

[Serializable]
public class RejoinSuccessMessage
{
    public string type = MessageTypes.RejoinSuccess;
    public string playerId;
}

[Serializable]
public class ErrorMessage
{
    public string type = MessageTypes.Error;
    public string message;
}

[Serializable]
public class PlayerInfo
{
    public string id;
    public string name;
    public int score;
    public int tableSide;
}

[Serializable]
public class PlayerListMessage
{
    public string type = MessageTypes.PlayerList;
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
    public string type = MessageTypes.GameState;
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
    public string type = MessageTypes.VoteUpdate;
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
    public string type = MessageTypes.PongFrame;
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
    public string type = MessageTypes.PongEvent;
    public string eventName;
    public string playerId;
    public string scoredOn;
    public int livesLeft;
}

// ──────────────────────────────────────────────
//  Fibbage (client → server)
// ──────────────────────────────────────────────

[Serializable]
public class SubmitBluffMessage
{
    public string type;
    public string bluff;
}

[Serializable]
public class FibbageVoteMessage
{
    public string type;
    public int choiceIndex;
}

// ──────────────────────────────────────────────
//  Fibbage (server → client)
// ──────────────────────────────────────────────

[Serializable]
public class FibbageChoiceInfo
{
    public string text;
    public bool isTruth;
    public string authorId;
}

[Serializable]
public class FibbageResultEntry
{
    public string id;
    public string name;
    public int pointsThisRound;
    public int totalScore;
    public string bluff;
    public int fooledCount;
    public bool pickedTruth;
}

// ──────────────────────────────────────────────
//  Speed Draw (client → server)
// ──────────────────────────────────────────────

[Serializable]
public class DrawStrokeMessage
{
    public string type;
    public float[] points; // [x0, y0, x1, y1, ...] normalized 0-1
    public string color;
}

[Serializable]
public class DrawGuessMessage
{
    public string type;
    public int choiceIndex;
}

// ──────────────────────────────────────────────
//  Speed Draw (server → client)
// ──────────────────────────────────────────────

[Serializable]
public class DrawStrokeBroadcast
{
    public string type = MessageTypes.DrawStroke;
    public string playerId;
    public float[] points;
    public string color;
}

[Serializable]
public class SpeedDrawLabelInfo
{
    public string text;
    public bool isCorrect;
}

// ──────────────────────────────────────────────
//  Hot Potato (client → server)
// ──────────────────────────────────────────────

[Serializable]
public class PotatoPassMessage
{
    public string type;
    public string direction; // "left", "right", "across"
}

// ──────────────────────────────────────────────
//  Hot Potato (server → client)
// ──────────────────────────────────────────────

[Serializable]
public class PotatoPlayerState
{
    public string id;
    public string name;
    public int strikes;
    public bool alive;
    public bool hasPotato;
    public int seatIndex;
}

[Serializable]
public class PotatoStateMessage
{
    public string type = MessageTypes.GameState;
    public string gameType = "hot_potato";
    public string state;
    public int timer;
    public PotatoPlayerState[] players;
    public string holderId;
    public int round;
    public int totalRounds;
}

[Serializable]
public class PotatoEventMessage
{
    public string type = MessageTypes.PotatoEvent;
    public string eventName; // "passed", "exploded", "eliminated"
    public string fromId;
    public string toId;
    public string playerId;
}

// ──────────────────────────────────────────────
//  Caption Contest (client → server)
// ──────────────────────────────────────────────

[Serializable]
public class SubmitCaptionMessage
{
    public string type;
    public string caption;
}

[Serializable]
public class CaptionVoteMessage
{
    public string type;
    public string captionId;
}

// ──────────────────────────────────────────────
//  Caption Contest (server → client)
// ──────────────────────────────────────────────

[Serializable]
public class CaptionInfo
{
    public string id;
    public string text;
}

[Serializable]
public class CaptionResultInfo
{
    public string id;
    public string name;
    public string caption;
    public int votes;
    public int score;
}

// ──────────────────────────────────────────────
//  Price is Close (client → server)
// ──────────────────────────────────────────────

[Serializable]
public class SubmitGuessMessage
{
    public string type;
    public float guess;
}

// ──────────────────────────────────────────────
//  Price is Close (server → client)
// ──────────────────────────────────────────────

[Serializable]
public class PriceGuessResult
{
    public string id;
    public string name;
    public float guess;
    public float correctPrice;
    public int score;
}

// ──────────────────────────────────────────────
//  Shared game state extensions for new games
// ──────────────────────────────────────────────

[Serializable]
public class FibbageStateMessage
{
    public string type = MessageTypes.GameState;
    public string gameType = "fibbage";
    public string state;
    public int timer;
    public int round;
    public int totalRounds;
    public string prompt;
    public FibbageChoiceInfo[] choices;
    public FibbageResultEntry[] results;
    public PlayerInfo[] players;
}

[Serializable]
public class SpeedDrawStateMessage
{
    public string type = MessageTypes.GameState;
    public string gameType = "speed_draw";
    public string state;
    public int timer;
    public int round;
    public int totalRounds;
    public string prompt;
    public SpeedDrawLabelInfo[] labels;
    public PlayerInfo[] players;
}

[Serializable]
public class CaptionContestStateMessage
{
    public string type = MessageTypes.GameState;
    public string gameType = "caption_contest";
    public string state;
    public int timer;
    public int round;
    public int totalRounds;
    public string imageUrl;
    public CaptionInfo[] captions;
    public CaptionResultInfo[] results;
    public PlayerInfo[] players;
}

[Serializable]
public class PriceIsCloseStateMessage
{
    public string type = MessageTypes.GameState;
    public string gameType = "price_is_close";
    public string state;
    public int timer;
    public int round;
    public int totalRounds;
    public string title;
    public string description;
    public string imageUrl;
    public string videoUrl;
    public string unit;
    public float correctPrice;
    public PriceGuessResult[] results;
    public PlayerInfo[] players;
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
