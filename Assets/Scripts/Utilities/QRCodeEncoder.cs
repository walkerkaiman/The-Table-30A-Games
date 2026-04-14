using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Self-contained QR code encoder. Zero external dependencies.
/// Byte-mode encoding, versions 1-5, ECC level L.
/// Handles payloads up to ~106 characters (URLs, WiFi strings, etc.).
/// </summary>
public static class QRCodeEncoder
{
    //  Version table: { dataCodewords, ecCodewords } — ECC Level L, 1 block each
    private static readonly int[,] V = {
        { 19,  7 }, { 34, 10 }, { 55, 15 }, { 80, 20 }, { 108, 26 }
    };

    // Alignment pattern center coordinates per version (empty for V1)
    private static readonly int[][] AL = {
        new int[0], new[]{ 6, 18 }, new[]{ 6, 22 }, new[]{ 6, 26 }, new[]{ 6, 30 }
    };

    // Remainder bits after data+EC placement
    private static readonly int[] REM = { 0, 7, 7, 7, 7 };

    // GF(256) with primitive polynomial x^8+x^4+x^3+x^2+1
    private static readonly int[] EXP = new int[512];
    private static readonly int[] LOG = new int[256];

    static QRCodeEncoder()
    {
        int x = 1;
        for (int i = 0; i < 255; i++)
        {
            EXP[i] = x;
            LOG[x] = i;
            x <<= 1;
            if (x >= 256) x ^= 0x11D;
        }
        for (int i = 255; i < 512; i++)
            EXP[i] = EXP[i - 255];
    }

    // ════════════════════════════════════════════
    //  PUBLIC API
    // ════════════════════════════════════════════

    /// <summary>
    /// Encode a string into a QR code module matrix.
    /// True = dark module, false = light module.
    /// </summary>
    public static bool[,] Encode(string text)
    {
        byte[] data = Encoding.UTF8.GetBytes(text);

        int ver = -1;
        for (int v = 0; v < V.GetLength(0); v++)
        {
            if (data.Length <= V[v, 0] - 2) { ver = v; break; }
        }
        if (ver < 0)
            throw new ArgumentException($"Data too long ({data.Length} bytes, max 106).");

        int version = ver + 1;
        int size = 17 + version * 4;
        int dataCW = V[ver, 0];
        int ecCW = V[ver, 1];

        byte[] cw = EncodeBytes(data, dataCW);
        byte[] ec = ReedSolomon(cw, ecCW);

        var payload = new byte[dataCW + ecCW];
        Buffer.BlockCopy(cw, 0, payload, 0, dataCW);
        Buffer.BlockCopy(ec, 0, payload, dataCW, ecCW);

        var mod = new bool[size, size];
        var fun = new bool[size, size];

        DrawFunction(mod, fun, size, version);
        DrawData(mod, fun, payload, size, REM[ver]);

        int bestMask = 0, bestPen = int.MaxValue;
        for (int m = 0; m < 8; m++)
        {
            var tmp = (bool[,])mod.Clone();
            Mask(tmp, fun, size, m);
            WriteFormat(tmp, size, m);
            int p = Penalty(tmp, size);
            if (p < bestPen) { bestPen = p; bestMask = m; }
        }

        Mask(mod, fun, size, bestMask);
        WriteFormat(mod, size, bestMask);
        return mod;
    }

    /// <summary>
    /// Render a module matrix to a Texture2D with a quiet zone.
    /// </summary>
    public static Texture2D ToTexture(bool[,] matrix, int scale = 10,
        Color? dark = null, Color? light = null, int quiet = 4)
    {
        Color dk = dark ?? Color.black;
        Color lt = light ?? Color.white;
        int n = matrix.GetLength(0);
        int total = (n + quiet * 2) * scale;
        var tex = new Texture2D(total, total, TextureFormat.RGB24, false)
        {
            filterMode = FilterMode.Point
        };

        var px = new Color[total * total];
        for (int i = 0; i < px.Length; i++) px[i] = lt;

        for (int r = 0; r < n; r++)
        {
            for (int c = 0; c < n; c++)
            {
                if (!matrix[r, c]) continue;
                int ox = (quiet + c) * scale;
                int oy = (quiet + n - 1 - r) * scale;
                for (int dy = 0; dy < scale; dy++)
                    for (int dx = 0; dx < scale; dx++)
                        px[(oy + dy) * total + ox + dx] = dk;
            }
        }

        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    // ════════════════════════════════════════════
    //  DATA ENCODING (byte mode)
    // ════════════════════════════════════════════

    private static byte[] EncodeBytes(byte[] data, int capacity)
    {
        var bits = new List<bool>(capacity * 8 + 16);
        Put(bits, 0b0100, 4);            // mode: byte
        Put(bits, data.Length, 8);        // character count
        foreach (byte b in data)
            Put(bits, b, 8);

        int max = capacity * 8;
        int term = Math.Min(4, max - bits.Count);
        for (int i = 0; i < term; i++) bits.Add(false);
        while (bits.Count % 8 != 0) bits.Add(false);

        var cw = new byte[capacity];
        int byteCount = bits.Count / 8;
        for (int i = 0; i < byteCount && i < capacity; i++)
        {
            int b = 0;
            for (int j = 0; j < 8; j++)
                b = (b << 1) | (bits[i * 8 + j] ? 1 : 0);
            cw[i] = (byte)b;
        }

        bool alt = false;
        for (int i = byteCount; i < capacity; i++)
        {
            cw[i] = (byte)(alt ? 0x11 : 0xEC);
            alt = !alt;
        }
        return cw;
    }

    private static void Put(List<bool> bits, int val, int count)
    {
        for (int i = count - 1; i >= 0; i--)
            bits.Add(((val >> i) & 1) == 1);
    }

    // ════════════════════════════════════════════
    //  REED-SOLOMON over GF(256)
    // ════════════════════════════════════════════

    private static int GfMul(int a, int b)
    {
        return (a == 0 || b == 0) ? 0 : EXP[LOG[a] + LOG[b]];
    }

    private static byte[] ReedSolomon(byte[] data, int nsym)
    {
        var gen = new int[] { 1 };
        for (int i = 0; i < nsym; i++)
        {
            var next = new int[gen.Length + 1];
            for (int g = 0; g < gen.Length; g++)
            {
                next[g] ^= gen[g];
                next[g + 1] ^= GfMul(gen[g], EXP[i]);
            }
            gen = next;
        }

        var msg = new int[data.Length + nsym];
        for (int i = 0; i < data.Length; i++) msg[i] = data[i];

        for (int i = 0; i < data.Length; i++)
        {
            int c = msg[i];
            if (c != 0)
                for (int j = 1; j < gen.Length; j++)
                    msg[i + j] ^= GfMul(gen[j], c);
        }

        var ec = new byte[nsym];
        for (int i = 0; i < nsym; i++) ec[i] = (byte)msg[data.Length + i];
        return ec;
    }

    // ════════════════════════════════════════════
    //  FUNCTION PATTERNS
    // ════════════════════════════════════════════

    private static void DrawFunction(bool[,] mod, bool[,] fun, int sz, int ver)
    {
        Finder(mod, fun, 0, 0, sz);
        Finder(mod, fun, 0, sz - 7, sz);
        Finder(mod, fun, sz - 7, 0, sz);

        // Timing patterns (row 6, col 6)
        for (int i = 8; i < sz - 8; i++)
        {
            bool d = i % 2 == 0;
            Set(mod, fun, 6, i, d);
            Set(mod, fun, i, 6, d);
        }

        // Dark module
        Set(mod, fun, sz - 8, 8, true);

        // Alignment pattern(s)
        var al = AL[ver - 1];
        for (int i = 0; i < al.Length; i++)
            for (int j = 0; j < al.Length; j++)
                if (!fun[al[i], al[j]])
                    Align(mod, fun, al[i], al[j]);

        // Reserve format info areas
        for (int i = 0; i < 8; i++)
        {
            Reserve(fun, 8, i, sz);
            Reserve(fun, 8, sz - 1 - i, sz);
            Reserve(fun, i, 8, sz);
            Reserve(fun, sz - 1 - i, 8, sz);
        }
        Reserve(fun, 8, 8, sz);
        Reserve(fun, 8, sz - 8, sz);
    }

    private static void Finder(bool[,] mod, bool[,] fun, int row, int col, int sz)
    {
        for (int r = -1; r <= 7; r++)
        {
            for (int c = -1; c <= 7; c++)
            {
                int rr = row + r, cc = col + c;
                if (rr < 0 || rr >= sz || cc < 0 || cc >= sz) continue;
                bool dark;
                if (r < 0 || r > 6 || c < 0 || c > 6) dark = false;
                else if (r == 0 || r == 6 || c == 0 || c == 6) dark = true;
                else if (r >= 2 && r <= 4 && c >= 2 && c <= 4) dark = true;
                else dark = false;
                mod[rr, cc] = dark;
                fun[rr, cc] = true;
            }
        }
    }

    private static void Align(bool[,] mod, bool[,] fun, int cr, int cc)
    {
        for (int dr = -2; dr <= 2; dr++)
            for (int dc = -2; dc <= 2; dc++)
            {
                bool d = Math.Abs(dr) == 2 || Math.Abs(dc) == 2 || (dr == 0 && dc == 0);
                mod[cr + dr, cc + dc] = d;
                fun[cr + dr, cc + dc] = true;
            }
    }

    private static void Set(bool[,] mod, bool[,] fun, int r, int c, bool dark)
    {
        mod[r, c] = dark;
        fun[r, c] = true;
    }

    private static void Reserve(bool[,] fun, int r, int c, int sz)
    {
        if (r >= 0 && r < sz && c >= 0 && c < sz)
            fun[r, c] = true;
    }

    // ════════════════════════════════════════════
    //  DATA PLACEMENT (zigzag)
    // ════════════════════════════════════════════

    private static void DrawData(bool[,] mod, bool[,] fun, byte[] payload, int sz, int remBits)
    {
        var bits = new List<bool>(payload.Length * 8 + remBits);
        foreach (byte b in payload)
            for (int i = 7; i >= 0; i--)
                bits.Add(((b >> i) & 1) == 1);
        for (int i = 0; i < remBits; i++) bits.Add(false);

        int idx = 0;
        bool up = true;

        for (int right = sz - 1; right >= 0; right -= 2)
        {
            if (right == 6) right--;
            for (int vert = 0; vert < sz; vert++)
            {
                int row = up ? sz - 1 - vert : vert;
                for (int j = 0; j <= 1; j++)
                {
                    int col = right - j;
                    if (col < 0 || col >= sz) continue;
                    if (fun[row, col]) continue;
                    if (idx < bits.Count)
                        mod[row, col] = bits[idx++];
                }
            }
            up = !up;
        }
    }

    // ════════════════════════════════════════════
    //  MASKING
    // ════════════════════════════════════════════

    private static void Mask(bool[,] mod, bool[,] fun, int sz, int mask)
    {
        for (int r = 0; r < sz; r++)
            for (int c = 0; c < sz; c++)
                if (!fun[r, c] && MaskBit(mask, r, c))
                    mod[r, c] = !mod[r, c];
    }

    private static bool MaskBit(int m, int r, int c)
    {
        switch (m)
        {
            case 0: return (r + c) % 2 == 0;
            case 1: return r % 2 == 0;
            case 2: return c % 3 == 0;
            case 3: return (r + c) % 3 == 0;
            case 4: return (r / 2 + c / 3) % 2 == 0;
            case 5: return r * c % 2 + r * c % 3 == 0;
            case 6: return (r * c % 2 + r * c % 3) % 2 == 0;
            case 7: return ((r + c) % 2 + r * c % 3) % 2 == 0;
            default: return false;
        }
    }

    // ════════════════════════════════════════════
    //  FORMAT INFORMATION
    // ════════════════════════════════════════════

    private static void WriteFormat(bool[,] mod, int sz, int mask)
    {
        int bits = FmtBits(mask);

        // First copy (near top-left finder)
        int[] r1 = { 8, 8, 8, 8, 8, 8, 8, 8, 7, 5, 4, 3, 2, 1, 0 };
        int[] c1 = { 0, 1, 2, 3, 4, 5, 7, 8, 8, 8, 8, 8, 8, 8, 8 };

        for (int i = 0; i < 15; i++)
        {
            bool dark = ((bits >> (14 - i)) & 1) == 1;
            mod[r1[i], c1[i]] = dark;
        }

        // Second copy (near other finders)
        for (int i = 0; i < 7; i++)
        {
            bool dark = ((bits >> (14 - i)) & 1) == 1;
            mod[sz - 1 - i, 8] = dark;
        }
        for (int i = 7; i < 15; i++)
        {
            bool dark = ((bits >> (14 - i)) & 1) == 1;
            mod[8, sz - 15 + i] = dark;
        }
    }

    private static int FmtBits(int mask)
    {
        int data = (0b01 << 3) | mask; // ECC level L = 01
        int rem = data << 10;
        for (int i = 14; i >= 10; i--)
            if ((rem & (1 << i)) != 0)
                rem ^= 0x537 << (i - 10);
        return ((data << 10) | (rem & 0x3FF)) ^ 0x5412;
    }

    // ════════════════════════════════════════════
    //  PENALTY SCORING
    // ════════════════════════════════════════════

    private static int Penalty(bool[,] mod, int sz)
    {
        int pen = 0;

        // Rule 1: runs of 5+ same-colored modules
        for (int r = 0; r < sz; r++)
        {
            int run = 1;
            for (int c = 1; c < sz; c++)
            {
                if (mod[r, c] == mod[r, c - 1]) run++;
                else { if (run >= 5) pen += run - 2; run = 1; }
            }
            if (run >= 5) pen += run - 2;
        }
        for (int c = 0; c < sz; c++)
        {
            int run = 1;
            for (int r = 1; r < sz; r++)
            {
                if (mod[r, c] == mod[r - 1, c]) run++;
                else { if (run >= 5) pen += run - 2; run = 1; }
            }
            if (run >= 5) pen += run - 2;
        }

        // Rule 2: 2x2 blocks of same color
        for (int r = 0; r < sz - 1; r++)
            for (int c = 0; c < sz - 1; c++)
                if (mod[r, c] == mod[r, c + 1] &&
                    mod[r, c] == mod[r + 1, c] &&
                    mod[r, c] == mod[r + 1, c + 1])
                    pen += 3;

        // Rule 3: finder-like patterns (1011101 0000 or 0000 1011101)
        bool[] pat1 = { true, false, true, true, true, false, true, false, false, false, false };
        bool[] pat2 = { false, false, false, false, true, false, true, true, true, false, true };

        for (int r = 0; r < sz; r++)
        {
            for (int c = 0; c <= sz - 11; c++)
            {
                if (MatchRow(mod, r, c, pat1) || MatchRow(mod, r, c, pat2)) pen += 40;
            }
        }
        for (int c = 0; c < sz; c++)
        {
            for (int r = 0; r <= sz - 11; r++)
            {
                if (MatchCol(mod, r, c, pat1) || MatchCol(mod, r, c, pat2)) pen += 40;
            }
        }

        // Rule 4: dark module proportion
        int dark = 0;
        for (int r = 0; r < sz; r++)
            for (int c = 0; c < sz; c++)
                if (mod[r, c]) dark++;
        int pct = dark * 100 / (sz * sz);
        pen += (Math.Abs(pct - 50) / 5) * 10;

        return pen;
    }

    private static bool MatchRow(bool[,] m, int r, int c, bool[] p)
    {
        for (int i = 0; i < p.Length; i++)
            if (m[r, c + i] != p[i]) return false;
        return true;
    }

    private static bool MatchCol(bool[,] m, int r, int c, bool[] p)
    {
        for (int i = 0; i < p.Length; i++)
            if (m[r + i, c] != p[i]) return false;
        return true;
    }
}
