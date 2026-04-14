using UnityEngine;

/// <summary>
/// Splits the main camera into two halves for tabletop projection.
/// The bottom half renders normally ("This Side" players, tableSide 0).
/// The top half renders rotated 180° ("That Side" players, tableSide 1),
/// so text and game elements are readable from both sides of the table.
///
/// Also locks the output to 1920x1080 at the target aspect ratio (16:9).
///
/// Attach this to the Main Camera in any scene that needs a mirrored display.
/// The component automatically creates a second camera at runtime.
/// </summary>
[RequireComponent(typeof(Camera))]
public class MirroredTableCamera : MonoBehaviour
{
    private const int TARGET_WIDTH = 1920;
    private const int TARGET_HEIGHT = 1080;

    [Tooltip("When false, the mirroring is disabled and the main camera renders full-screen.")]
    [SerializeField] private bool mirrorEnabled = true;

    [Tooltip("Gap between the two halves in viewport units (0-0.05). 0 = no gap.")]
    [SerializeField] [Range(0f, 0.05f)] private float splitGap = 0.005f;

    private Camera _mainCamera;
    private Camera _mirrorCamera;
    private GameObject _mirrorCameraGO;

    private Rect _originalViewport;
    private bool _wasEnabled;

    private void Awake()
    {
        _mainCamera = GetComponent<Camera>();
        _originalViewport = _mainCamera.rect;
        EnforceResolution();
    }

    private void Start()
    {
        if (mirrorEnabled)
            EnableMirror();
    }

    private void OnDestroy()
    {
        DisableMirror();
    }

    public bool MirrorEnabled
    {
        get => mirrorEnabled;
        set
        {
            if (value == mirrorEnabled) return;
            mirrorEnabled = value;
            if (mirrorEnabled) EnableMirror();
            else DisableMirror();
        }
    }

    private void EnableMirror()
    {
        if (_mirrorCameraGO != null) return;

        float halfGap = splitGap * 0.5f;

        // Main camera → bottom half
        _mainCamera.rect = new Rect(0f, 0f, 1f, 0.5f - halfGap);

        // Mirror camera → top half, rotated 180° around Z
        _mirrorCameraGO = new GameObject("MirrorCamera");
        _mirrorCameraGO.transform.SetParent(transform);
        _mirrorCameraGO.transform.localPosition = Vector3.zero;
        _mirrorCameraGO.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);
        _mirrorCameraGO.transform.localScale = Vector3.one;

        _mirrorCamera = _mirrorCameraGO.AddComponent<Camera>();
        var listener = _mirrorCameraGO.GetComponent<AudioListener>();
        if (listener != null) Destroy(listener);
        CopyCameraSettings(_mainCamera, _mirrorCamera);
        _mirrorCamera.rect = new Rect(0f, 0.5f + halfGap, 1f, 0.5f - halfGap);

        _wasEnabled = true;
    }

    private void DisableMirror()
    {
        if (_mirrorCameraGO != null)
        {
            Destroy(_mirrorCameraGO);
            _mirrorCameraGO = null;
            _mirrorCamera = null;
        }

        _mainCamera.rect = _originalViewport;
        _wasEnabled = false;
    }

    private void LateUpdate()
    {
        if (!_wasEnabled || _mirrorCamera == null) return;

        // Keep mirror camera in sync if main camera settings change at runtime
        _mirrorCamera.orthographic = _mainCamera.orthographic;
        _mirrorCamera.orthographicSize = _mainCamera.orthographicSize;
        _mirrorCamera.fieldOfView = _mainCamera.fieldOfView;
        _mirrorCamera.nearClipPlane = _mainCamera.nearClipPlane;
        _mirrorCamera.farClipPlane = _mainCamera.farClipPlane;
        _mirrorCamera.backgroundColor = _mainCamera.backgroundColor;
        _mirrorCamera.cullingMask = _mainCamera.cullingMask;
        _mirrorCamera.depth = _mainCamera.depth;
    }

    private static void CopyCameraSettings(Camera src, Camera dst)
    {
        dst.orthographic = src.orthographic;
        dst.orthographicSize = src.orthographicSize;
        dst.fieldOfView = src.fieldOfView;
        dst.nearClipPlane = src.nearClipPlane;
        dst.farClipPlane = src.farClipPlane;
        dst.backgroundColor = src.backgroundColor;
        dst.clearFlags = src.clearFlags;
        dst.cullingMask = src.cullingMask;
        dst.depth = src.depth;
        dst.allowHDR = src.allowHDR;
        dst.allowMSAA = src.allowMSAA;
    }

    private static void EnforceResolution()
    {
        Screen.SetResolution(TARGET_WIDTH, TARGET_HEIGHT, FullScreenMode.FullScreenWindow);
    }
}
