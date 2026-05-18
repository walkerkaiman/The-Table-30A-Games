using System;
using System.Collections.Generic;

/// <summary>
/// Loads authored clue wording templates from
/// <c>StreamingAssets/GameContent/treasure_hunter/clue_templates.json</c> via
/// <see cref="GameContentLoader{T}"/> and provides fast lookup by <see cref="ClueFact.FactType"/>.
///
/// Template JSON schema:
/// <code>
/// {
///   "templates": [
///     { "factType": "TrapWarning",    "text": "Beware of a trap in the {roomName}, to the {direction}.", "flavor": "spooky" },
///     { "factType": "ExitLocation",   "text": "The exit lies in the {roomName}.",                        "flavor": "" },
///     ...
///   ]
/// }
/// </code>
/// </summary>
public class ClueTemplateLoader
{
    [Serializable]
    public class ClueTemplate
    {
        public string factType;  // matches ClueFact.FactType enum name
        public string text;      // placeholder text: {roomName}, {direction}, {colorSequence}
        public string flavor;    // optional flavour tag for filtering
    }

    [Serializable]
    private class TemplateRoot
    {
        public ClueTemplate[] templates;
    }

    private readonly Dictionary<string, List<ClueTemplate>> _byType =
        new Dictionary<string, List<ClueTemplate>>(StringComparer.OrdinalIgnoreCase);

    private bool _loaded;

    public bool Load()
    {
        var root = GameContentLoader<ClueTemplate>.LoadRaw<TemplateRoot>("treasure_hunter", "clue_templates.json");
        if (root == null || root.templates == null || root.templates.Length == 0)
        {
            GameLog.Warn("ClueTemplateLoader: clue_templates.json not found or empty. Clues will use fallback text.");
            return false;
        }

        foreach (var t in root.templates)
        {
            if (string.IsNullOrEmpty(t.factType)) continue;
            if (!_byType.TryGetValue(t.factType, out var list))
            {
                list = new List<ClueTemplate>();
                _byType[t.factType] = list;
            }
            list.Add(t);
        }

        _loaded = true;
        GameLog.Prompt($"ClueTemplateLoader: loaded {root.templates.Length} templates");
        return true;
    }

    /// <summary>
    /// Pick a random template for the given fact type. Returns a fallback if none found.
    /// </summary>
    public ClueTemplate GetRandom(ClueFact.FactType factType, System.Random rng)
    {
        string key = factType.ToString();
        if (_byType.TryGetValue(key, out var list) && list.Count > 0)
            return list[rng.Next(list.Count)];

        return new ClueTemplate
        {
            factType = key,
            text = FallbackText(factType),
        };
    }

    private static string FallbackText(ClueFact.FactType t)
    {
        return t switch
        {
            ClueFact.FactType.TrapWarning    => "Watch out! There's a trap to the {direction} in the {roomName}.",
            ClueFact.FactType.ExitLocation   => "The exit is in the {roomName}.",
            ClueFact.FactType.GoldLocation   => "Gold can be found to the {direction} in the {roomName}.",
            ClueFact.FactType.PlateColorOrder => "The rune plates must be pressed in this order: {colorSequence}.",
            _                                => "You have a secret clue: [{factType}].",
        };
    }
}
