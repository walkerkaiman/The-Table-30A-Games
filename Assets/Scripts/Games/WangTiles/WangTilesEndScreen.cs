using TMPro;
using UnityEngine;

/// <summary>
/// Post-game snapshot QR overlay for the shared table display.
///
/// Auto-spawns its own world-space SpriteRenderer when no manual targets are
/// assigned and parks it in the center of the camera viewport during ShowingQR.
/// Phones independently get the URL through the broadcast game_state message.
/// </summary>
public class WangTilesEndScreen : MonoBehaviour
{
    [Header("QR Target (optional — auto-spawned if both null)")]
    [SerializeField] private UnityEngine.UI.RawImage qrImage;
    [SerializeField] private SpriteRenderer qrSprite;
    [SerializeField] private TMP_Text urlTextUi;
    [SerializeField] private TextMeshPro urlTextWorld;
    [SerializeField] private TextMeshPro headlineTextWorld;
    [SerializeField] private string headlineLabel = "Tapestry Saved";

    [Header("Auto-spawned placement")]
    [Tooltip("Camera the world-space QR follows. Defaults to Camera.main.")]
    [SerializeField] private Camera trackingCamera;
    [Tooltip("World-space height of the auto-spawned QR sprite.")]
    [SerializeField] private float qrWorldSize = 3.5f;
    [Tooltip("Z position of the auto-spawned QR. Negative = closer to camera.")]
    [SerializeField] private float zOffset = -2f;

    [Header("QR Appearance")]
    [SerializeField] private int pixelsPerModule = 16;
    [SerializeField] private Color darkColor = Color.black;
    [SerializeField] private Color lightColor = Color.white;
    [SerializeField] private int quietZone = 4;

    private Texture2D _qrTex;
    private Sprite _qrSpriteAsset;
    private GameObject _autoQrGO;
    private SpriteRenderer _autoBackdropSr;

    private static event System.Action<string> SnapshotReady;
    /// <summary>Manager calls this when the PNG is written and URL is known.</summary>
    public static void NotifySnapshotReady(string url) => SnapshotReady?.Invoke(url);

    private void Awake()
    {
        SetVisible(false);
    }

    private void OnEnable()
    {
        SnapshotReady += OnSnapshotReady;
        WangTilesManager.PhaseChanged += OnPhaseChanged;
    }

    private void OnDisable()
    {
        SnapshotReady -= OnSnapshotReady;
        WangTilesManager.PhaseChanged -= OnPhaseChanged;
    }

    private void OnDestroy()
    {
        if (_qrSpriteAsset != null) Destroy(_qrSpriteAsset);
        if (_qrTex != null) Destroy(_qrTex);
        if (_autoQrGO != null) Destroy(_autoQrGO);
    }

    private void Update()
    {
        if (_autoQrGO == null) return;
        Camera cam = trackingCamera != null ? trackingCamera : Camera.main;
        if (cam == null) return;
        Vector3 camPos = cam.transform.position;
        _autoQrGO.transform.position = new Vector3(camPos.x, camPos.y, camPos.z + Mathf.Abs(zOffset));
    }

    private void OnPhaseChanged(WangTilesManager.Phase phase)
    {
        if (phase == WangTilesManager.Phase.Painting)
            SetVisible(false);
    }

    private void OnSnapshotReady(string url)
    {
        try
        {
            if (_qrTex != null) Destroy(_qrTex);
            if (_qrSpriteAsset != null) Destroy(_qrSpriteAsset);

            bool[,] matrix = QRCodeEncoder.Encode(url);
            _qrTex = QRCodeEncoder.ToTexture(matrix, pixelsPerModule, darkColor, lightColor, quietZone);

            if (qrImage != null) qrImage.texture = _qrTex;

            if (qrSprite != null)
            {
                _qrSpriteAsset = Sprite.Create(
                    _qrTex,
                    new Rect(0, 0, _qrTex.width, _qrTex.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
                qrSprite.sprite = _qrSpriteAsset;
            }
            else if (qrImage == null)
            {
                AutoSpawnSprite();
            }

            if (urlTextUi != null) urlTextUi.text = url;
            if (urlTextWorld != null) urlTextWorld.text = url;
            if (headlineTextWorld != null) headlineTextWorld.text = headlineLabel;

            SetVisible(true);
        }
        catch (System.Exception ex)
        {
            GameLog.Error($"WangTilesEndScreen failed to render QR: {ex}");
        }
    }

    private void AutoSpawnSprite()
    {
        if (_qrTex == null) return;
        if (_autoQrGO == null)
        {
            _autoQrGO = new GameObject("AutoEndQR");
            _autoQrGO.transform.SetParent(transform, false);

            // Backdrop: a white rounded-ish quad behind the QR so dark camera backgrounds
            // don't tint the dark modules. Use a simple white sprite at the same size.
            var backdropGO = new GameObject("Backdrop");
            backdropGO.transform.SetParent(_autoQrGO.transform, false);
            backdropGO.transform.localPosition = new Vector3(0f, 0f, 0.01f);
            _autoBackdropSr = backdropGO.AddComponent<SpriteRenderer>();
            _autoBackdropSr.sprite = WhiteSprite();
            _autoBackdropSr.color = new Color(1f, 1f, 1f, 1f);
            _autoBackdropSr.transform.localScale = new Vector3(qrWorldSize * 1.05f, qrWorldSize * 1.05f, 1f);
            _autoBackdropSr.sortingOrder = 199;
        }

        _qrSpriteAsset = Sprite.Create(
            _qrTex,
            new Rect(0, 0, _qrTex.width, _qrTex.height),
            new Vector2(0.5f, 0.5f),
            _qrTex.width / qrWorldSize);

        var qrChild = _autoQrGO.transform.Find("QR");
        SpriteRenderer sr;
        if (qrChild == null)
        {
            var qrGO = new GameObject("QR");
            qrGO.transform.SetParent(_autoQrGO.transform, false);
            sr = qrGO.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 200;
        }
        else
        {
            sr = qrChild.GetComponent<SpriteRenderer>();
        }
        sr.sprite = _qrSpriteAsset;
        qrSprite = sr;
    }

    private static Sprite _whiteSprite;
    private static Sprite WhiteSprite()
    {
        if (_whiteSprite != null) return _whiteSprite;
        var t = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        var px = new Color32[16];
        for (int i = 0; i < 16; i++) px[i] = new Color32(255, 255, 255, 255);
        t.SetPixels32(px);
        t.Apply();
        _whiteSprite = Sprite.Create(t, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        return _whiteSprite;
    }

    private void SetVisible(bool visible)
    {
        if (qrImage != null) qrImage.enabled = visible;
        if (qrSprite != null) qrSprite.enabled = visible;
        if (_autoBackdropSr != null) _autoBackdropSr.enabled = visible;
        SetTmpVisible(urlTextUi, visible);
        SetTmpVisibleWorld(urlTextWorld, visible);
        SetTmpVisibleWorld(headlineTextWorld, visible);
    }

    private static void SetTmpVisible(TMP_Text t, bool visible)
    {
        if (t != null) t.enabled = visible;
    }

    private static void SetTmpVisibleWorld(TextMeshPro t, bool visible)
    {
        if (t == null) return;
        var mr = t.GetComponent<MeshRenderer>();
        if (mr != null) mr.enabled = visible;
    }
}
