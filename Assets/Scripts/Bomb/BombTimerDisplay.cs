using TMPro;
using UnityEngine;

// Shows the last seconds before the explosion, on every peer.
// Must not sit on the timer text itself: hiding the text would stop this Update.
public class BombTimerDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text _timerText;
    [SerializeField] private float _visibleBelow = 10f;

    private void Awake()
    {
        if (_timerText != null && _timerText.gameObject == gameObject)
            Debug.LogError("[BombTimerDisplay] Put this component on another object than the timer text.");
    }

    private void Update()
    {
        if (_timerText == null)
            return;

        float remaining = BombManager.Instance != null ? BombManager.Instance.RemainingTime : -1f;
        bool visible = remaining > 0f && remaining <= _visibleBelow;

        if (_timerText.gameObject.activeSelf != visible)
            _timerText.gameObject.SetActive(visible);

        if (visible)
            _timerText.text = Mathf.CeilToInt(remaining).ToString();
    }
}