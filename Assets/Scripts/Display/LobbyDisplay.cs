using UnityEngine;

/// <summary>
/// Attach to any GameObject in the Lobby scene that should be hidden
/// while a game is playing (e.g., QR code canvas, lobby UI).
/// The object reappears when returning to Lobby or Game Select.
///
/// If a Canvas component is present on this GameObject, it toggles
/// Canvas.enabled so the GameObject stays active and keeps receiving events.
/// Otherwise it adds/uses a CanvasGroup to toggle visibility without
/// deactivating the GameObject (which would unsubscribe from events).
/// </summary>
public class LobbyDisplay : MonoBehaviour
{
    private Canvas _canvas;
    private CanvasGroup _canvasGroup;

    private void Awake()
    {
        _canvas = GetComponent<Canvas>();
        if (_canvas == null)
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void OnEnable()
    {
        GameCoordinator.StateChanged += OnStateChanged;
    }

    private void OnDisable()
    {
        GameCoordinator.StateChanged -= OnStateChanged;
    }

    private void OnStateChanged(GameCoordinator.CoordinatorState state)
    {
        bool visible = state != GameCoordinator.CoordinatorState.InGame;

        if (_canvas != null)
        {
            _canvas.enabled = visible;
        }
        else
        {
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.blocksRaycasts = visible;
            _canvasGroup.interactable = visible;
        }
    }
}
