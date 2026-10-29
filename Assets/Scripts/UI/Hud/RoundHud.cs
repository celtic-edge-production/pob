using TMPro;
using UnityEngine;
using UnityEngine.UI;

// This machine's HUD during a round: its trap lives and who holds the bomb (the lives of every
// player are shown over their heads by TrapLivesMarkers). Every peer, polled
// from replicated state (PlayerCombat.TrapLives, BombManager.bombHolderId), so it works the same
// for the host and the clients. Every field is optional: assign the parts the canvas has.
public class RoundHud : MonoBehaviour
{
    [Tooltip("Shown only while this machine's character is in play (hidden in phase 1).")]
    [SerializeField] private GameObject _root;

    [Header("Trap lives")]
    [Tooltip("One image per life, first life first (the hearts of the health bar).")]
    [SerializeField] private Image[] _lifeImages;
    [Tooltip("Sprite of a life still there. Empty: the images keep their sprite.")]
    [SerializeField] private Sprite _lifeFull;
    [Tooltip("Sprite of a lost life. Empty: a lost life is hidden.")]
    [SerializeField] private Sprite _lifeLost;

    [Header("Bomb holder")]
    [Tooltip("Shown while this machine's player holds the bomb.")]
    [SerializeField] private GameObject _localHolderIndicator;
    [Tooltip("The holder's name.")]
    [SerializeField] private TMP_Text _holderName;
    [Tooltip("The holder's character sprite.")]
    [SerializeField] private Image _holderPortrait;
    [Tooltip("Shown while someone holds the bomb (frame of the name and portrait).")]
    [SerializeField] private GameObject _holderPanel;

    // Last values shown: the UI is only touched when they change.
    private int _shownLives = -1;
    private ulong _shownHolder = ulong.MaxValue - 1;
    private bool _shownInPlay = true;

    private void Update()
    {
        PlayerCharacter local = Players.Local != null ? Players.Local.Character : null;
        bool inPlay = local != null && local.IsVisible && BombManager.Instance != null;

        if (inPlay != _shownInPlay)
        {
            _shownInPlay = inPlay;
            if (_root != null)
                _root.SetActive(inPlay);
        }

        if (!inPlay)
            return;

        int lives = local.TryGetComponent(out PlayerCombat combat) ? combat.TrapLives.Value : 0;
        if (lives != _shownLives)
            ShowLives(lives);

        ulong holder = BombManager.Instance.bombHolderId.Value;
        if (holder != _shownHolder)
            ShowHolder(holder, local.OwnerClientId);
    }

    private void ShowLives(int lives)
    {
        _shownLives = lives;

        if (_lifeImages != null)
        {
            for (int i = 0; i < _lifeImages.Length; i++)
            {
                Image image = _lifeImages[i];
                if (image == null)
                    continue;

                bool alive = i < lives;

                if (alive && _lifeFull != null)
                    image.sprite = _lifeFull;
                else if (!alive && _lifeLost != null)
                    image.sprite = _lifeLost;

                image.enabled = alive || _lifeLost != null;
            }
        }
    }

    private void ShowHolder(ulong holderId, ulong localId)
    {
        _shownHolder = holderId;
        bool someone = holderId != BombManager.NoPlayer;

        if (_localHolderIndicator != null)
            _localHolderIndicator.SetActive(someone && holderId == localId);

        if (_holderPanel != null)
            _holderPanel.SetActive(someone);

        if (!someone)
            return;

        if (_holderName != null)
            _holderName.text = Players.NameOf(holderId);

        if (_holderPortrait != null)
        {
            PlayerCharacter character = Players.TryGet(holderId, out PlayerIdentity player) ? player.Character : null;
            SpriteRenderer sprite = character != null ? character.GetComponent<SpriteRenderer>() : null;
            if (sprite != null)
                _holderPortrait.sprite = sprite.sprite;
        }
    }
}
