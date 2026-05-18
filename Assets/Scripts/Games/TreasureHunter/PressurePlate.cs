using UnityEngine;

/// <summary>
/// An individual pressure plate trigger zone. Reports to its parent
/// <see cref="PressurePlatePuzzle"/> when an explorer steps on or off it.
///
/// Prefab setup (child of PressurePlatePuzzlePrefab):
///   • Add a <c>Collider2D</c> with <c>isTrigger = true</c>.
///   • Add a <c>SpriteRenderer</c> for the plate visual (color matches <see cref="plateColor"/>).
///   • Optionally add a pressed/unpressed sprite swap via <see cref="pressedSprite"/> /
///     <see cref="unpressedSprite"/>.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class PressurePlate : MonoBehaviour
{
    [Header("Identity")]
    [Tooltip("Color of this plate (for clue generation and visual theming).")]
    public Color plateColor = Color.white;

    [Header("Visuals")]
    [SerializeField] private SpriteRenderer plateSprite;
    [SerializeField] private Sprite pressedSprite;
    [SerializeField] private Sprite unpressedSprite;
    [Tooltip("Optional particle burst when pressed.")]
    [SerializeField] private GameObject pressVfx;

    public bool IsPressed { get; private set; }

    private PressurePlatePuzzle _puzzle;

    public void SetPuzzle(PressurePlatePuzzle puzzle) => _puzzle = puzzle;

    private void Start()
    {
        if (plateSprite != null) plateSprite.color = plateColor;
        UpdateSprite();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (IsPressed) return;
        var explorer = other.GetComponent<Explorer>() ?? other.GetComponentInParent<Explorer>();
        if (explorer == null || explorer.IsDown) return;

        IsPressed = true;
        UpdateSprite();
        if (pressVfx != null) pressVfx.SetActive(true);
        _puzzle?.OnPlateStateChanged();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsPressed) return;
        var explorer = other.GetComponent<Explorer>() ?? other.GetComponentInParent<Explorer>();
        if (explorer == null) return;

        IsPressed = false;
        UpdateSprite();
        _puzzle?.OnPlateStateChanged();
    }

    private void UpdateSprite()
    {
        if (plateSprite == null) return;
        if (IsPressed && pressedSprite != null) plateSprite.sprite = pressedSprite;
        else if (!IsPressed && unpressedSprite != null) plateSprite.sprite = unpressedSprite;
    }
}
