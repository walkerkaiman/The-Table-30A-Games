using System;

/// <summary>
/// Shared leaderboard utilities for sorting standings and logging game-over results.
/// </summary>
public static class Leaderboard
{
    public static PlayerInfo[] GetSorted()
    {
        var standings = PlayerManager.Instance.GetAllPlayerInfos();
        Array.Sort(standings, (a, b) => b.score.CompareTo(a.score));
        return standings;
    }

    public static void LogStandings(string gameName, PlayerInfo[] sorted)
    {
        GameLog.Divider();
        GameLog.Game($"══ {gameName} GAME OVER ══");
        for (int i = 0; i < sorted.Length; i++)
        {
            string marker = i == 0 ? "  <<< WINNER" : "";
            GameLog.Game($"  {OrdinalOf(i + 1)}: \"{sorted[i].name}\" — {sorted[i].score} pts{marker}");
        }
        GameLog.Divider();
    }

    public static string OrdinalOf(int n)
    {
        return n switch { 1 => "1st", 2 => "2nd", 3 => "3rd", _ => $"{n}th" };
    }
}
