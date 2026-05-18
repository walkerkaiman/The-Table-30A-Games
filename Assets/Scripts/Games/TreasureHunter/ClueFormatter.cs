using System.Collections.Generic;

/// <summary>
/// Merges a <see cref="ClueFact"/> with a <see cref="ClueTemplateLoader.ClueTemplate"/> to
/// produce the final clue text string sent to a player's phone.
///
/// Placeholder replacements:
///   <c>{roomName}</c>, <c>{direction}</c>, <c>{colorSequence}</c>, <c>{factType}</c>
/// </summary>
public static class ClueFormatter
{
    public static string Format(ClueFact fact, ClueTemplateLoader.ClueTemplate template)
    {
        if (template == null || string.IsNullOrEmpty(template.text)) return "(clue unavailable)";

        string text = template.text;
        text = text.Replace("{roomName}",      string.IsNullOrEmpty(fact.roomName)      ? "the dungeon" : fact.roomName);
        text = text.Replace("{direction}",     string.IsNullOrEmpty(fact.direction)     ? "somewhere"   : fact.direction);
        text = text.Replace("{colorSequence}", string.IsNullOrEmpty(fact.colorSequence) ? "unknown"     : fact.colorSequence);
        text = text.Replace("{factType}",      fact.factType.ToString());
        return text;
    }

    /// <summary>
    /// Format all facts for a single player into a string array ready for <see cref="TreasureHunterBriefingMessage"/>.
    /// </summary>
    public static string[] FormatAll(List<ClueFact> facts, ClueTemplateLoader templates, System.Random rng)
    {
        if (facts == null || facts.Count == 0) return new string[] { "You have no clues this round." };
        var result = new string[facts.Count];
        for (int i = 0; i < facts.Count; i++)
            result[i] = Format(facts[i], templates.GetRandom(facts[i].factType, rng));
        return result;
    }
}
