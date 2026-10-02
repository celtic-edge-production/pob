using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using static GameRoundManager;

public class Scoreboard : MonoBehaviour
{
    [SerializeField] private Sprite _HeartImage;
    [SerializeField] private Sprite _SkullImage;

    [SerializeField] public Image[] PlayerImage;
    [SerializeField] private Image[] _StatusImageList;
    [SerializeField] private TMP_Text[] _TotalTextScoreList;
    [SerializeField] private GameObject[] _RoundTextScoreList;
    [SerializeField] private GameRoundManager _GameRoundManager;

    private Animator _Animator;

    // State of the roundScore animation
    private int _MaxPlayerRoundScoreIndex;
    private ulong _CurrentPlayerId;
    private int _CurrentPlayerRoundScoreIndex;
    private int _PlayerIdInList;

    private void Awake()
    {
        _Animator = GetComponent<Animator>();
        _Animator.enabled = false;
        _Animator.SetBool("IsOpening", false);
        _Animator.SetBool("IsToggle", false);

        _CurrentPlayerRoundScoreIndex = 0;
        _MaxPlayerRoundScoreIndex = 0;
        _PlayerIdInList = 0;

        // Bug corrige (race condition) : "UpdateDisplayScore()" lisait ici les
        // NetworkVariable/NetworkList de _GameRoundManager (PlayerIds, PlayerTotalScores)
        // des Awake(), qui s'execute tres tot au chargement de la scene - potentiellement
        // avant que NGO ait fini de synchroniser ces variables reseau sur ce client.
        // Cet appel etait aussi redondant : SwitchScoreboardStateClientRpc() (dans
        // GameRoundManager.cs) appelle deja UpdateDisplayScore() plus tard, une fois
        // la manche bien avancee et les donnees surement pretes. Retire l'appel precoce
        // au profit du seul appel tardif, deja fiable.
    }

    public void SwitchScoreboardState()
    {
        _Animator.enabled = true;
        _Animator.SetBool("IsToggle", true);
        _Animator.SetBool("IsOpening", true);
    }

    private void OnScoreboardOpen()
    {
        StartCoroutine(StartRoundScoreAnimation());
    }

    private IEnumerator StartRoundScoreAnimation()
    {
        yield return new WaitForSeconds(3.0f);
        StartRoundScoreAnimation(0);
    }

    public void SetUpPlayerStateImages()
    {
        bool isSurvived = false;

        foreach (var playerRoundScore in _GameRoundManager.PlayerRoundScores.Value)
        {
            // Get the index of the playerId in the PlayerIds list
            int index = _GameRoundManager.PlayerIds.IndexOf(playerRoundScore.Key);

            foreach (var roundScore in playerRoundScore.Value)
            {
                Debug.Log("roundScore.StringValue -> " + roundScore.StringValue);
                if (roundScore.StringValue.Equals("Survived the round"))
                {
                    // Set Heart Image
                    _StatusImageList[index].sprite = _HeartImage;
                    isSurvived = true;
                    break;
                } 
            }

            if (isSurvived)
            {
                isSurvived = false;
                continue;
            }
            // Set Skull Image
            _StatusImageList[index].sprite = _SkullImage;
        }
    }

    private void OnScoreboardClose()
    {
        Debug.Log("Scoreboard closed");
        _Animator.SetBool("IsToggle", false);

        // Reset State of the roundScore animation
        _CurrentPlayerRoundScoreIndex = 0;
        _MaxPlayerRoundScoreIndex = 0;
        _PlayerIdInList = 0;

        for (int i = 0; i < _RoundTextScoreList.Length; i++)
        {
            _RoundTextScoreList[i].GetComponent<TMP_Text>().text = "";
            _RoundTextScoreList[i].SetActive(false);
            PlayerImage[i].GetComponent<Image>().gameObject.SetActive(false);
        }

        StartCoroutine(_GameRoundManager.ChangePhase());
        StartCoroutine(CleanScoreboard());
    }

    private IEnumerator CleanScoreboard()
    {
        yield return new WaitForSeconds(3.0f);
        _Animator.enabled = false;
        gameObject.SetActive(false);
    }

    public void UpdateDisplayScore()
    {
        for (int i = 0; i < _RoundTextScoreList.Length; i++)
        {
            _RoundTextScoreList[i].SetActive(true);
            _RoundTextScoreList[i].GetComponent<TMP_Text>().text = "";
            //var animator = _RoundTextScoreList[i].GetComponent<Animator>();
            //if (animator != null && animator.runtimeAnimatorController != null)
            //{
            //    animator.SetBool("IsAnimated", false);
            //}
        }

        for (int i = 0; i < _GameRoundManager.PlayerIds.Count; i++)
        {
            ulong playerId = _GameRoundManager.PlayerIds[i];

            _TotalTextScoreList[i].gameObject.SetActive(true);
            _StatusImageList[i].gameObject.SetActive(true);
            PlayerImage[i].GetComponent<Image>().gameObject.SetActive(true);

            // Check if the playerTotalScores[playerId exist if not set the value to 0
            if (!_GameRoundManager.PlayerTotalScores.Value.ContainsKey(playerId))
            {
                _GameRoundManager.PlayerTotalScores.Value[playerId] = 0;
                _TotalTextScoreList[i].text = playerId.ToString() + " score : " + 0;
            }
            else
            {
                _TotalTextScoreList[i].text = playerId.ToString() + " score : " + _GameRoundManager.PlayerTotalScores.Value[playerId].ToString();
            }
        }
    }

    private void SetText()
    {
        // Set text
        TMP_Text text = _RoundTextScoreList[_PlayerIdInList].GetComponent<TMP_Text>();
        IntStringPair element = _GameRoundManager.PlayerRoundScores.Value[_CurrentPlayerId][_CurrentPlayerRoundScoreIndex];
        text.text = element.StringValue + " " + element.IntValue + " Points !";

        // Reset the animator state and start the animation
        Animator animator = _RoundTextScoreList[_PlayerIdInList].GetComponent<Animator>();

        if (animator != null)
        {
            animator.Rebind();  // Resets the Animator to its initial state
            animator.Update(0f);
            animator.SetBool("IsAnimated", true);
        } else
        {
            Debug.LogError("Cannot get the animator in setText()");
        }
    }
    private void StartRoundScoreAnimation(int start)
    {
        // Find a player with at least one roundScore
        for (int i = start; i < _GameRoundManager.PlayerIds.Count; i++)
        {
            ulong playerId = _GameRoundManager.PlayerIds[i];

            if (_GameRoundManager.PlayerRoundScores.Value.ContainsKey(playerId) &&
                _GameRoundManager.PlayerRoundScores.Value[playerId].Count > 0)
            {
                _PlayerIdInList = i;
                _CurrentPlayerId = playerId;
                _MaxPlayerRoundScoreIndex = _GameRoundManager.PlayerRoundScores.Value[_CurrentPlayerId].Count;

                SetText();

                return;
            }
        }
        Debug.Log("No player with roundScore found");
        CloseScoreboard();
    }

    public void OnTextAnimationCompleted()
    {
        // Stop the animation
        _RoundTextScoreList[_PlayerIdInList].GetComponent<Animator>().SetBool("IsAnimated", false);

        _CurrentPlayerRoundScoreIndex++;

        if (_CurrentPlayerRoundScoreIndex < _MaxPlayerRoundScoreIndex)
        {
            SetText();
        }
        else
        {
            _CurrentPlayerRoundScoreIndex = 0;
            if (_PlayerIdInList + 1 < _GameRoundManager.PlayerIds.Count)
            {
                StartRoundScoreAnimation(_PlayerIdInList + 1);
            }
            else
            {
                CloseScoreboard();
                return;
            }
        }
    }

    private void CloseScoreboard()
    {
        _Animator.SetBool("IsOpening", false);
    }
}
