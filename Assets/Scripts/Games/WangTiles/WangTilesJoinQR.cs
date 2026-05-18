using TMPro;
using UnityEngine;

/// <summary>
/// Persistent join-QR display anchored to the camera viewport while Wang Tiles is
/// in the Painting phase. Lets anyone scan and start drawing without waiting for the
/// game to end. Hidden during Ending/ShowingQR so the big snapshot QR can take over.
///
/// Auto-spawns its own world-space <see cref="SpriteRenderer"/> if no manual target
/// is assigned, so the scene works out of the box. The renderer is parented to the
/// camera so it stays in the same screen position as the mosaic pans.
///
/// Encodes the same URL the lobby uses (http://{LocalIP}:{Port}).
/// </summary>
public class WangTilesJoinQR : MonoBehaviour
{
    [Header("QR Target (optional — auto-spawned if both null)")]
    [SerializeField] private UnityEngine.UI.RawImage qrImageUi;
    [SerializeField] private SpriteRenderer qrSprite;
    [SerializeField] private TMP_Text captionTextUi;
    [SerializeField] private TextMeshPro captionTextWorld;
    [SerializeField] private string captionLabel = "Scan to join";

    [Header("Auto-spawned placement")]
    [Tooltip("Camera the world-space QR follows. Defaults to Camera.main.")]
    [SerializeField] private Camera trackingCamera;
    [Tooltip("World-space size (height & width) of the auto-spawned QR sprite.")]
    [SerializeField] private float qrWorldSize = 1.2f;
    [Tooltip("Right inset from the camera's right edge, in world units.")]
    [SerializeField] private float rightInset = 0.85f;
    [Tooltip("Bottom inset from the camera's bottom edge, in world units.")]
    [SerializeField] private float bottomInset = 0.85f;
    [Tooltip("Z position of the auto-spawned QR. Negative = closer to camera.")]
    [SerializeField] private float zOffset = -1f;

    [Header("QR Appearance")]
    [SerializeField] private int pixelsPerModule = 8;
    [SerializeField] private Color darkColor = Color.black;
    [SerializeField] private Color lightColor = Color.white;
    [SerializeField] private int quietZone = 4;

    private Texture2D _qrTex;
    private Sprite _qrSpriteAsset;
    private GameObject _autoQrGO;

    private void Start()
    {
        BuildQr();
        if (qrSprite == null && qrImageUi == null)
            AutoSpawnSprite();
        ApplyVisibility(WangTilesManager.Phase.Painting);
    }

    private void OnEnable()
    {
        WangTilesManager.PhaseChanged += ApplyVisibility;
    }

    private void OnDisable()
    {
        WangTilesManager.PhaseChanged -= ApplyVisibility;
    }

    private void OnDestroy()
    {
        if (_qrSpriteAsset != null) Destroy(_qrSpriteAsset);
        if (_qrTex != null) Destroy(_qrTex);
        if (_autoQrGO != null) Destroy(_autoQrGO);
    }

    private void Update()
    {
        // Keep the auto-spawned sprite anchored to the camera's bottom-right corner.
        // Re-anchoring every frame is cheap (vs. parenting + needing to undo
        // camera roll/rotation from the mirror camera).
        if (_autoQrGO == null) return;
        Camera cam = trackingCamera != null ? trackingCamera : Camera.main;
        if (cam == null || !cam.orthographic) return;

        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        Vector3 camPos = cam.transform.position;
        Vector3 target = new Vector3(
            camPos.x + halfW - rightInset,
            camPos.y - halfH + bottomInset,
            camPos.z + Mathf.Abs(zOffset));
        _autoQrGO.transform.position = target;
    }

    private void BuildQr()
    {
        string ip = NetworkManager.Instance != null ? NetworkManager.Instance.LocalIP : "127.0.0.1";
        int port = NetworkManager.Instance != null ? NetworkManager.Instance.Port : 7777;
        string url = $"http://{ip}:{port}";

        try
        {
            bool[,] matrix = QRCodeEncoder.Encode(url);
            _qrTex = QRCodeEncoder.ToTexture(matrix, pixelsPerModule, darkColor, lightColor, quietZone);

            if (qrImageUi != null) qrImageUi.texture = _qrTex;

            if (qrSprite != null)
            {
                _qrSpriteAsset = Sprite.Create(
                    _qrTex,
                    new Rect(0, 0, _qrTex.width, _qrTex.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
                qrSprite.sprite = _qrSpriteAsset;
            }

            if (captionTextUi != null) captionTextUi.text = captionLabel;
            if (captionTextWorld != null) captionTextWorld.text = captionLabel;

            GameLog.Game($"Wang Tiles join QR built for {url}");
        }
        catch (System.Exception ex)
        {
            GameLog.Error($"WangTilesJoinQR failed to build QR: {ex}");
        }
    }

    private void AutoSpawnSprite()
    {
        if (_qrTex == null) return;
        _autoQrGO = new GameObject("AutoJoinQR");
        _autoQrGO.transform.SetParent(transform, false);
        _qrSpriteAsset = Sprite.Create(
            _qrTex,
            new Rect(0, 0, _qrTex.width, _qrTex.height),
            new Vector2(0.5f, 0.5f),
            _qrTex.width / qrWorldSize); // sized so the sprite is qrWorldSize world units across
        var sr = _autoQrGO.AddComponent<SpriteRenderer>();
        sr.sprite = _qrSpriteAsset;
        sr.sortingOrder = 100;
        qrSprite = sr;
    }

    private void ApplyVisibility(WangTilesManager.Phase phase)
    {
        bool visible = phase == WangTilesManager.Phase.Painting;
        if (qrImageUi != null) qrImageUi.enabled = visible;
        if (qrSprite != null) qrSprite.enabled = visible;
        if (captionTextUi != null) captionTextUi.enabled = visible;
        if (captionTextWorld != null)
        {
            var mr = captionTextWorld.GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = visible;
        }
    }
}
