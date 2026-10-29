using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
    private List<ScoreLine> _CurrentPlayerScores = new();
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

    public void SetUpPlayerStateImages(ulong[] survivorIds)
    {
        for (int i = 0; i < _GameRoundManager.Scores.PlayerIds.Count; i++)
        {
            bool survived = System.Array.IndexOf(survivorIds, _GameRoundManager.Scores.PlayerIds[i]) >= 0;
            _StatusImageList[i].sprite = survived ? _HeartImage : _SkullImage;
        }
    }

    private void OnScoreboardClose()
    {
        _Animator.SetBool("IsToggle", false);

        // Reset State of the roundScore animation
        _CurrentPlayerRoundScoreIndex = 0;
        _MaxPlayerRoundScoreIndex = 0;
        _PlayerIdInList = 0;
        _CurrentPlayerScores.Clear();

        for (int i = 0; i < _RoundTextScoreList.Length; i++)
        {
            _RoundTextScoreList[i].GetComponent<TMP_Text>().text = "";
            _RoundTextScoreList[i].SetActive(false);
            PlayerImage[i].GetComponent<Image>().gameObject.SetActive(false);
        }

        _GameRoundManager.Display.OnScoreboardClosed();
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
        }

        for (int i = 0; i < _GameRoundManager.Scores.PlayerIds.Count; i++)
        {
            ulong playerId = _GameRoundManager.Scores.PlayerIds[i];

            _TotalTextScoreList[i].gameObject.SetActive(true);
            _StatusImageList[i].gameObject.SetActive(true);
            PlayerImage[i].GetComponent<Image>().gameObject.SetActive(true);

            // Read-only: the scores belong to the server.
            int gold = _GameRoundManager.Scores.GoldOf(playerId);
            _TotalTextScoreList[i].text = $"{gold} gold";
        }
    }

    private void SetText()
    {
        // Set text
        TMP_Text text = _RoundTextScoreList[_PlayerIdInList].GetComponent<TMP_Text>();
        ScoreLine line = _CurrentPlayerScores[_CurrentPlayerRoundScoreIndex];
        string sign = line.Points > 0 ? "+" : "";
        text.text = $"{line.Reason} {sign}{line.Points} gold";

        // Reset the animator state and start the animation
        Animator animator = _RoundTextScoreList[_PlayerIdInList].GetComponent<Animator>();

        if (animator != null)
        {
            animator.Rebind();  // Resets the Animator to its initial state
            animator.Update(0f);
            animator.SetBool("IsAnimated", true);
        }
        else
        {
            Debug.LogError("Cannot get the animator in setText()");
        }
    }
    private void StartRoundScoreAnimation(int start)
    {
        // Find a player with at least one roundScore
        for (int i = start; i < _GameRoundManager.Scores.PlayerIds.Count; i++)
        {
            ulong playerId = _GameRoundManager.Scores.PlayerIds[i];

            // Taken once per player: the lines played one after the other cannot change under it.
            List<ScoreLine> lines = _GameRoundManager.Scores.GetLinesOf(playerId);

            if (lines.Count > 0)
            {
                _PlayerIdInList = i;
                _CurrentPlayerScores = lines;
                _MaxPlayerRoundScoreIndex = lines.Count;

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
            if (_PlayerIdInList + 1 < _GameRoundManager.Scores.PlayerIds.Count)
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
