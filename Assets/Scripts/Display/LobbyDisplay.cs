using UnityEngine;

/// <summary>
/// Attach to any GameObject in the Lobby scene that should be hidden
/// while a game is playing (e.g., QR code canvas, lobby UI).
/// The object reappears when returning to Lobby or Game Select.
///
/// If a Canvas component is present on this GameObject, it toggles
/// Canvas.enabled so the GameObject stays active and keeps receiving events.
/// Otherwise it toggles a CanvasGroup alpha + blocksRaycasts as a fallback.
/// </summary>
public class LobbyDisplay : MonoBehaviour
{
    private Canvas _canvas;

    private void Awake()
    {
        _canvas = GetComponent<Canvas>();
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
            gameObject.SetActive(visible);
        }
    }
}
