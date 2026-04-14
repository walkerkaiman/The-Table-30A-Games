using TMPro;
using UnityEngine;

/// <summary>
/// Telephone-specific table display. Extends GameTableDisplay with:
///   - During active phases: shows step progress
///   - During Reveal: renders each chain entry on screen
///     * Text entries shown as large readable text
///     * Drawing entries rendered as strokes onto a Texture2D
///   - Author name and chain progress indicators
/// </summary>
public class TelephoneTableDisplay : GameTableDisplay
{
    [Header("Telephone — Reveal UI")]
    [SerializeField] private TextMeshPro revealContentText;
    [SerializeField] private TextMeshPro authorText;
    [SerializeField] private TextMeshPro progressText;
    [SerializeField] private SpriteRenderer drawingRenderer;

    [Header("Drawing Settings")]
    [SerializeField] private int textureSize = 512;
    [SerializeField] private Color strokeColor = Color.white;
    [SerializeField] private Color backgroundColor = new Color(0.04f, 0.1f, 0.19f, 1f);
    [SerializeField] private int strokeThickness = 4;

    private MeshRenderer _revealContentRenderer;
    private MeshRenderer _authorRenderer;
    private MeshRenderer _progressRenderer;

    private Texture2D _drawingTex;
    private bool _revealActive;

    protected override void Awake()
    {
        base.Awake();

        _revealContentRenderer = CacheRenderer(revealContentText);
        _authorRenderer = CacheRenderer(authorText);
        _progressRenderer = CacheRenderer(progressText);

        SetRendererEnabled(_revealContentRenderer, false);
        SetRendererEnabled(_authorRenderer, false);
        SetRendererEnabled(_progressRenderer, false);
        if (drawingRenderer != null) drawingRenderer.enabled = false;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        TelephoneManager.RevealEntryChanged += OnRevealEntry;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        TelephoneManager.RevealEntryChanged -= OnRevealEntry;
    }

    private void OnDestroy()
    {
        if (_drawingTex != null)
            Destroy(_drawingTex);
    }

    protected override void OnPhaseChanged(string gameType, string phase, int timer)
    {
        if (gameType != "telephone") return;

        bool isReveal = phase == "Reveal" || phase == "RevealPause";
        bool isDone = phase == "Done";

        _revealActive = isReveal;

        SetRevealVisible(isReveal);

        if (isDone)
        {
            SetBaseTextVisible(false);
            SetRendererEnabled(_phaseRenderer, true);
            if (phaseText != null) phaseText.text = FormatPhase(phase);
        }
        else
        {
            SetBaseTextVisible(!isReveal);
        }
    }

    private void OnRevealEntry(TelephoneRevealEntry entry, int chainIndex, int totalChains, int entryIndex, int chainLength)
    {
        if (!_revealActive) return;

        if (progressText != null)
        {
            progressText.text = $"Chain {chainIndex + 1} of {totalChains}  —  {entryIndex + 1}/{chainLength}";
            SetRendererEnabled(_progressRenderer, true);
        }

        if (authorText != null)
        {
            authorText.text = entry.playerName ?? "";
            SetRendererEnabled(_authorRenderer, true);
        }

        if (entry.entryType == "drawing")
        {
            ShowDrawing(entry.strokes);
            if (revealContentText != null)
            {
                revealContentText.text = "";
                SetRendererEnabled(_revealContentRenderer, false);
            }
        }
        else
        {
            HideDrawing();
            if (revealContentText != null)
            {
                revealContentText.text = entry.content ?? "";
                SetRendererEnabled(_revealContentRenderer, true);
            }
        }
    }

    private void ShowDrawing(TelephoneStroke[] strokes)
    {
        if (drawingRenderer == null) return;

        EnsureTexture();
        ClearTexture();

        if (strokes != null)
            RenderStrokes(strokes);

        _drawingTex.Apply();
        drawingRenderer.sprite = Sprite.Create(
            _drawingTex,
            new Rect(0, 0, textureSize, textureSize),
            new Vector2(0.5f, 0.5f),
            100f
        );
        drawingRenderer.enabled = true;
    }

    private void HideDrawing()
    {
        if (drawingRenderer != null)
            drawingRenderer.enabled = false;
    }

    private void EnsureTexture()
    {
        if (_drawingTex != null && _drawingTex.width == textureSize) return;

        if (_drawingTex != null)
            Destroy(_drawingTex);

        _drawingTex = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
    }

    private void ClearTexture()
    {
        var pixels = _drawingTex.GetPixels();
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = backgroundColor;
        _drawingTex.SetPixels(pixels);
    }

    private void RenderStrokes(TelephoneStroke[] strokes)
    {
        int size = textureSize;
        foreach (var stroke in strokes)
        {
            if (stroke.points == null || stroke.points.Length < 4) continue;

            Color col = strokeColor;
            if (!string.IsNullOrEmpty(stroke.color))
                ColorUtility.TryParseHtmlString(stroke.color, out col);

            for (int i = 0; i + 3 < stroke.points.Length; i += 4)
            {
                int x0 = Mathf.Clamp(Mathf.RoundToInt(stroke.points[i] * (size - 1)), 0, size - 1);
                int y0 = Mathf.Clamp(Mathf.RoundToInt((1f - stroke.points[i + 1]) * (size - 1)), 0, size - 1);
                int x1 = Mathf.Clamp(Mathf.RoundToInt(stroke.points[i + 2] * (size - 1)), 0, size - 1);
                int y1 = Mathf.Clamp(Mathf.RoundToInt((1f - stroke.points[i + 3]) * (size - 1)), 0, size - 1);

                DrawThickLine(x0, y0, x1, y1, col);
            }
        }
    }

    private void DrawThickLine(int x0, int y0, int x1, int y1, Color col)
    {
        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        int half = strokeThickness / 2;

        while (true)
        {
            for (int ox = -half; ox <= half; ox++)
                for (int oy = -half; oy <= half; oy++)
                    SetPixelSafe(x0 + ox, y0 + oy, col);

            if (x0 == x1 && y0 == y1) break;

            int e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x0 += sx; }
            if (e2 < dx) { err += dx; y0 += sy; }
        }
    }

    private void SetPixelSafe(int x, int y, Color col)
    {
        if (x >= 0 && x < textureSize && y >= 0 && y < textureSize)
            _drawingTex.SetPixel(x, y, col);
    }

    private void SetRevealVisible(bool visible)
    {
        SetRendererEnabled(_revealContentRenderer, visible);
        SetRendererEnabled(_authorRenderer, visible);
        SetRendererEnabled(_progressRenderer, visible);
        if (drawingRenderer != null && !visible) drawingRenderer.enabled = false;
    }
}
