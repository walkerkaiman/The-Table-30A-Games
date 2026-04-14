using System.Collections.Generic;

/// <summary>
/// Shared collection utilities used across game managers and content loaders.
/// </summary>
public static class ListExtensions
{
    private static readonly System.Random _defaultRng = new System.Random();

    public static void Shuffle<T>(this IList<T> list, System.Random rng = null)
    {
        rng ??= _defaultRng;
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
