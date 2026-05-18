using System;
using System.Collections.Generic;
using System.Threading;

/// <summary>
/// Shared collection utilities used across game managers and content loaders.
/// </summary>
public static class ListExtensions
{
    [ThreadStatic] private static System.Random _threadRng;

    private static System.Random ThreadRng => _threadRng ??= new System.Random(
        System.Environment.TickCount ^ Thread.CurrentThread.ManagedThreadId);

    public static void Shuffle<T>(this IList<T> list, System.Random rng = null)
    {
        rng ??= ThreadRng;
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
