using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Structured, color-coded console output organized by subsystem.
/// Rich-text colors render in the Unity Editor console; in builds the tags are stripped
/// leaving clean [TAG] prefixes.
///
/// All methods are stripped from release builds via [Conditional] attributes — only
/// UNITY_EDITOR and DEVELOPMENT_BUILD compilations retain log calls (and the string
/// interpolation at their call sites).
///
/// Usage:
///   GameLog.Server("Listening on port 7777");
///   GameLog.State("Lobby -> ShowPrompt");
///   GameLog.Round("Answer from Alice (2/4)");
/// </summary>
public static class GameLog
{
    // ── Category loggers ─────────────────────────

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void Server(string msg) => Log("SERVER", "#999999", msg);

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void Net(string msg)    => Log("NET",    "#4fc3f7", msg);

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void Player(string msg) => Log("PLAYER", "#81c784", msg);

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void Game(string msg)   => Log("GAME",   "#ffb74d", msg);

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void Round(string msg)  => Log("ROUND",  "#ce93d8", msg);

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void State(string msg)  => Log("STATE",  "#fff176", msg);

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void Prompt(string msg) => Log("PROMPT", "#90a4ae", msg);

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void Warn(string msg)   => Debug.LogWarning($"[WARN] {msg}");

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void Error(string msg)  => Debug.LogError($"[ERROR] {msg}");

    // ── Visual separators ────────────────────────

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void Divider()
    {
        Debug.Log("<color=#555555>───────────────────────────────────────────────</color>");
    }

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void Banner(string line1, string line2 = null, string line3 = null)
    {
        Debug.Log("<color=#ffffff>╔═══════════════════════════════════════════════╗</color>");
        BannerLine(line1);
        if (line2 != null) BannerLine(line2);
        if (line3 != null) BannerLine(line3);
        Debug.Log("<color=#ffffff>╚═══════════════════════════════════════════════╝</color>");
    }

    // ── Internal ─────────────────────────────────

    private static void BannerLine(string text)
    {
        Debug.Log($"<color=#ffffff>║</color>  {text}");
    }

    private static void Log(string tag, string color, string msg)
    {
        Debug.Log($"<color={color}>[{tag}]</color> {msg}");
    }
}
