using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Every peer: the gold as it changes during a round, over the characters. A "+10" / "-10" rises
// over a player when he wins or loses gold (RoundDisplay relays the server's changes), and a mark
// stays over the players in danger (ScoreKeeper.IsInDanger, read from the replicated gold).
// Added to the GameRoundManager object by RoundDisplay: no scene setup needed.
public class GoldFeedback : MonoBehaviour
{
    [Header("Gold changes")]
    [SerializeField] private Color _gainColor = new Color(1f, 0.85f, 0.2f, 1f);
    [SerializeField] private Color _lossColor = new Color(0.9f, 0.25f, 0.25f, 1f);
    [SerializeField] private float _popupSize = 3f;
    [SerializeField] private float _popupDuration = 1.2f;
    [SerializeField] private float _popupRise = 0.8f;

    [Header("Danger mark")]
    [SerializeField] private string _dangerMark = "!";
    [SerializeField] private Color _dangerColor = new Color(0.9f, 0.25f, 0.25f, 1f);
    [SerializeField] private float _dangerSize = 4f;

    [Header("Placement")]
    [Tooltip("Height above the character's position (over the trap lives marker).")]
    [SerializeField] private float _heightAboveCharacter = 1.35f;
    [Tooltip("Order inside the character's sorting layer: drawn over the character.")]
    [SerializeField] private int _sortingOrder = 100;

    private ScoreKeeper _scores;
    private readonly Dictionary<ulong, TextMeshPro> _dangerMarks = new();

    private void Awake()
    {
        _scores = GetComponent<ScoreKeeper>();
    }

    private void OnDestroy()
    {
        foreach (TextMeshPro mark in _dangerMarks.Values)
        {
            if (mark != null)
                Destroy(mark.gameObject);
        }
        _dangerMarks.Clear();
    }

    // Every peer (RoundDisplay.ShowGoldChangeClientRpc).
    public void ShowChange(ulong playerId, int delta)
    {
        PlayerCharacter character = CharacterOf(playerId);
        if (character == null || !character.IsVisible)
            return;

        string text = delta > 0 ? $"+{delta}" : delta.ToString();
        TextMeshPro popup = CreateText("GoldPopup", text, delta > 0 ? _gainColor : _lossColor, _popupSize, character);
        popup.transform.position = AboveHead(character);

        StartCoroutine(RiseAndFade(popup, character));
    }

    // Follows the character: a loss on the last trap life comes with a respawn, and the popup left
    // where the hit happened was out of sight of the player's own camera, which follows him.
    private IEnumerator RiseAndFade(TextMeshPro popup, PlayerCharacter character)
    {
        Vector3 anchor = popup.transform.position;
        Color color = popup.color;

        for (float time = 0f; time < _popupDuration; time += Time.deltaTime)
        {
            if (popup == null)
                yield break;

            if (character != null)
                anchor = AboveHead(character);

            float progress = time / _popupDuration;
            popup.transform.position = anchor + Vector3.up * (_popupRise * progress);
            popup.color = new Color(color.r, color.g, color.b, 1f - progress * progress);
            yield return null;
        }

        if (popup != null)
            Destroy(popup.gameObject);
    }

    // The danger marks follow the characters; polled, the gold is replicated.
    private void LateUpdate()
    {
        if (_scores == null)
            return;

        foreach (PlayerCharacter character in Players.Characters)
        {
            ulong playerId = character.OwnerClientId;
            bool show = character.IsVisible && _scores.IsInDanger(playerId);

            if (!_dangerMarks.TryGetValue(playerId, out TextMeshPro mark) || mark == null)
            {
                if (!show)
                    continue;

                mark = CreateText("DangerMark", _dangerMark, _dangerColor, _dangerSize, character);
                _dangerMarks[playerId] = mark;
            }

            if (mark.gameObject.activeSelf != show)
                mark.gameObject.SetActive(show);

            if (show)
                mark.transform.position = AboveHead(character);
        }
    }

    private Vector3 AboveHead(PlayerCharacter character)
    {
        return character.transform.position + Vector3.up * _heightAboveCharacter;
    }

    private static PlayerCharacter CharacterOf(ulong playerId)
    {
        return Players.TryGet(playerId, out PlayerIdentity player) ? player.Character : null;
    }

    // World-space text, built in code so no prefab is needed (TMP's default font). Drawn in the
    // character's sorting layer: on the Default layer, it was hidden behind the level.
    private TextMeshPro CreateText(string objectName, string text, Color color, float size, PlayerCharacter character)
    {
        GameObject textObject = new GameObject(objectName);
        TextMeshPro tmp = textObject.AddComponent<TextMeshPro>();

        tmp.text = text;
        tmp.color = color;
        tmp.fontSize = size;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.rectTransform.sizeDelta = new Vector2(3f, 1f);
        tmp.sortingLayerID = character.TryGetComponent(out SpriteRenderer characterRenderer) ? characterRenderer.sortingLayerID : 0;
        tmp.sortingOrder = _sortingOrder;

        return tmp;
    }
}