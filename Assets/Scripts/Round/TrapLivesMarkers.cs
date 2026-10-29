using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Every peer: a heart and the number of trap lives left over every character in play, so everyone
// sees who is one trap away from losing gold. Read from the replicated PlayerCombat.TrapLives.
// Put it on the GameRoundManager prefab (or any object of the game scene) and give it the heart sprite.
public class TrapLivesMarkers : MonoBehaviour
{
    [SerializeField] private Sprite _heart;
    [Tooltip("Scale of the heart sprite in the world.")]
    [SerializeField] private float _heartScale = 0.5f;
    [SerializeField] private Color _numberColor = Color.white;
    [SerializeField] private float _numberSize = 3f;
    [Tooltip("Distance between the heart and the number, to its right.")]
    [SerializeField] private float _numberOffset = 0.3f;

    [Header("Placement")]
    [Tooltip("Height above the character's position.")]
    [SerializeField] private float _heightAboveCharacter = 0.9f;
    [Tooltip("Order inside the character's sorting layer: drawn over the character.")]
    [SerializeField] private int _sortingOrder = 100;

    private class Marker
    {
        public GameObject Root;
        public TextMeshPro Number;
        public int ShownLives = -1;
    }

    private readonly Dictionary<ulong, Marker> _markers = new();

    private void OnDestroy()
    {
        foreach (Marker marker in _markers.Values)
        {
            if (marker.Root != null)
                Destroy(marker.Root);
        }
        _markers.Clear();
    }

    private void LateUpdate()
    {
        foreach (PlayerCharacter character in Players.Characters)
        {
            if (!character.TryGetComponent(out PlayerCombat combat) || !character.TryGetComponent(out PlayerBomb bomb))
                continue;

            ulong playerId = character.OwnerClientId;
            bool show = character.IsVisible && bomb.IsAlive.Value;

            if (!_markers.TryGetValue(playerId, out Marker marker) || marker.Root == null)
            {
                if (!show)
                    continue;

                marker = CreateMarker(character);
                _markers[playerId] = marker;
            }

            if (marker.Root.activeSelf != show)
                marker.Root.SetActive(show);

            if (!show)
                continue;

            marker.Root.transform.position = character.transform.position + Vector3.up * _heightAboveCharacter;

            int lives = combat.TrapLives.Value;
            if (lives != marker.ShownLives)
            {
                marker.ShownLives = lives;
                marker.Number.text = lives.ToString();
            }
        }
    }

    // A heart sprite with the number to its right, built in code: no prefab needed. Drawn in the
    // character's sorting layer: on the Default layer, it was hidden behind the level.
    private Marker CreateMarker(PlayerCharacter character)
    {
        int sortingLayer = character.TryGetComponent(out SpriteRenderer characterRenderer) ? characterRenderer.sortingLayerID : 0;

        GameObject root = new GameObject("TrapLivesMarker");

        GameObject heart = new GameObject("Heart");
        heart.transform.SetParent(root.transform, false);
        heart.transform.localScale = Vector3.one * _heartScale;
        SpriteRenderer heartRenderer = heart.AddComponent<SpriteRenderer>();
        heartRenderer.sprite = _heart;
        heartRenderer.sortingLayerID = sortingLayer;
        heartRenderer.sortingOrder = _sortingOrder;

        GameObject numberObject = new GameObject("Lives");
        numberObject.transform.SetParent(root.transform, false);
        numberObject.transform.localPosition = new Vector3(_numberOffset, 0f, 0f);
        TextMeshPro number = numberObject.AddComponent<TextMeshPro>();
        number.color = _numberColor;
        number.fontSize = _numberSize;
        number.fontStyle = FontStyles.Bold;
        number.alignment = TextAlignmentOptions.Left;
        number.textWrappingMode = TextWrappingModes.NoWrap;
        number.rectTransform.sizeDelta = new Vector2(1f, 1f);
        number.rectTransform.pivot = new Vector2(0f, 0.5f);
        number.sortingLayerID = sortingLayer;
        number.sortingOrder = _sortingOrder;

        if (_heart == null)
            Debug.LogWarning("[TrapLivesMarkers] Assign the heart sprite in the Inspector.", this);

        return new Marker { Root = root, Number = number };
    }
}