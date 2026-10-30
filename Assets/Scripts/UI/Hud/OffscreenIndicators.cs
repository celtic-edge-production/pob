using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The bomb holder's help to find his next victim: on his screen only, an arrow on the screen edge
// for each other player out of sight, pointing at him and sliding along the edges as he moves.
// No portrait: the arrow takes the player's colour (the cursor colours).
// Put it on the HUD canvas, with a disabled arrow image as the template (its sprite points right).
public class OffscreenIndicators : MonoBehaviour
{
    [Tooltip("A disabled Image child of the canvas, its sprite pointing to the right. Copied once per player.")]
    [SerializeField] private RectTransform _arrowTemplate;
    [Tooltip("Distance from the screen edge, as a fraction of the screen (0.05 = 5 %).")]
    [SerializeField] private float _edgeMargin = 0.05f;

    [Tooltip("Tint each arrow with its player's colour. Off: every arrow keeps the template's colour.")]
    [SerializeField] private bool _usePlayerColours = true;

    private Canvas _canvas;
    // The arrows are created next to the template, and placed in its parent's space.
    private RectTransform _container;
    private readonly Dictionary<ulong, RectTransform> _arrows = new();
    private readonly HashSet<ulong> _shownThisFrame = new();

    private void Awake()
    {
        _canvas = GetComponentInParent<Canvas>();
        _container = _arrowTemplate != null ? _arrowTemplate.parent as RectTransform : null;

        if (_canvas == null || _container == null)
        {
            Debug.LogError("[OffscreenIndicators] Needs a canvas parent and an arrow template inside it.", this);
            return;
        }

        _arrowTemplate.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (_canvas == null || _container == null)
            return;

        PlayerCharacter local = Players.Local != null ? Players.Local.Character : null;
        Camera view = ViewOf(local);
        // Only while this machine's player holds the bomb.
        bool isHunting = local != null && local.IsVisible && view != null
            && local.TryGetComponent(out PlayerBomb localBomb) && localBomb.IsBombHolder();

        _shownThisFrame.Clear();

        if (isHunting)
        {
            foreach (PlayerCharacter character in Players.Characters)
            {
                if (character == local || !IsInPlay(character))
                    continue;

                if (TryPlace(character, view))
                    _shownThisFrame.Add(character.OwnerClientId);
            }
        }

        // Every arrow not placed this frame is hidden (not the holder, player on screen, dead, left, phase 1).
        foreach (KeyValuePair<ulong, RectTransform> arrow in _arrows)
        {
            bool show = _shownThisFrame.Contains(arrow.Key);
            if (arrow.Value != null && arrow.Value.gameObject.activeSelf != show)
                arrow.Value.gameObject.SetActive(show);
        }
    }

    // Returns false when the player is on screen (no arrow needed).
    private bool TryPlace(PlayerCharacter character, Camera view)
    {
        Vector3 viewport = view.WorldToViewportPoint(character.transform.position);
        bool isBehind = viewport.z < 0f;

        if (!isBehind && EdgeIndicator.IsOnScreen(viewport.x, viewport.y, 0f))
            return false;

        EdgeIndicator.PlaceOnEdge(viewport.x, viewport.y, isBehind, _edgeMargin,
            out float edgeX, out float edgeY, out float angle);

        RectTransform arrow = ArrowFor(character.OwnerClientId);

        Vector2 screenPoint = new Vector2(edgeX * Screen.width, edgeY * Screen.height);
        Camera canvasCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_container, screenPoint, canvasCamera, out Vector2 local);

        arrow.localPosition = local;
        arrow.localRotation = Quaternion.Euler(0f, 0f, angle);

        if (_usePlayerColours && arrow.TryGetComponent(out Image image))
            image.color = Utils.ColorTools.GetColorByID((int)character.OwnerClientId + 1); // as the cursors (SetupPlayer: id + 1)

        return true;
    }

    private RectTransform ArrowFor(ulong playerId)
    {
        if (_arrows.TryGetValue(playerId, out RectTransform arrow) && arrow != null)
            return arrow;

        arrow = Instantiate(_arrowTemplate, _container);
        arrow.name = $"OffscreenArrow {playerId}";
        _arrows[playerId] = arrow;
        return arrow;
    }

    private static bool IsInPlay(PlayerCharacter character)
    {
        return character.IsVisible && character.TryGetComponent(out PlayerBomb bomb) && bomb.IsAlive.Value;
    }

    // The camera this machine looks through: its character's camera in phase 2, else the main one.
    private static Camera ViewOf(PlayerCharacter local)
    {
        if (local != null && local._playerCamera != null && local._playerCamera.isActiveAndEnabled)
            return local._playerCamera;

        return Camera.main;
    }
}