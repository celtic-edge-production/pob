using UnityEngine;

// Waiting music between bombs, round music while a bomb is running, sped up at the end.
// Computed locally on every peer from BombManager.RemainingTime.
[RequireComponent(typeof(AudioSource))]
public class BombMusic : MonoBehaviour
{
    [SerializeField] private AudioClip _waitingMusic;
    [SerializeField] private AudioClip _roundMusic;
    [SerializeField] private float _volume = 0.5f;

    [Tooltip("The music speeds up during these last seconds, up to twice its normal pitch.")]
    [SerializeField] private float _speedUpBelow = 10f;

    private AudioSource _audioSource;

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        _audioSource.loop = true;
        _audioSource.volume = _volume;
        Play(_waitingMusic);
    }

    private void Update()
    {
        float remaining = BombManager.Instance != null ? BombManager.Instance.RemainingTime : -1f;
        bool bombRunning = remaining > 0f;

        AudioClip wanted = bombRunning ? _roundMusic : _waitingMusic;
        if (wanted != null && _audioSource.clip != wanted)
            Play(wanted);

        _audioSource.pitch = bombRunning && remaining < _speedUpBelow
            ? Mathf.Lerp(1f, 2f, (_speedUpBelow - remaining) / _speedUpBelow)
            : 1f;
    }

    private void Play(AudioClip clip)
    {
        if (clip == null)
            return;

        _audioSource.clip = clip;
        _audioSource.Play();
    }
}