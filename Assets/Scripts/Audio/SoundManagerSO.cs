using UnityEngine;

[CreateAssetMenu(menuName = "Audio/Sound Manager", fileName = "Sound Manager")]
public class SoundManagerSO : ScriptableObject {

    private static SoundManagerSO _Instance;

    public static SoundManagerSO Instance
    {
        get
        {
            if (_Instance == null)
                _Instance = Resources.Load<SoundManagerSO>("ScriptableObject/Sound/Sound Manager");
            return _Instance;
        }
    }

    public AudioSource SoundObject;

    [SerializeField]
    private static float _VolumeChangeMultiplier = 0.15f;

    [SerializeField]
    private static float _PitchChangeMultiplier = 0.15f;

    public static void PlaySoundFXClip(AudioClip clip, Vector3 soundPos, float volume)
    {
        AudioSource audio = Instantiate(Instance.SoundObject, soundPos, Quaternion.identity);
        float       pitch = Random.Range(1 - _PitchChangeMultiplier, 1 + _PitchChangeMultiplier);

        volume += Random.Range(-_VolumeChangeMultiplier, _VolumeChangeMultiplier);

        audio.clip   = clip;
        audio.volume = volume;
        audio.pitch  = pitch;

        audio.Play();
    }

    public static void PlaySoundFXClip(AudioClip[] clip, Vector3 soundPos, float volume)
    {
        AudioSource audio = Instantiate(Instance.SoundObject, soundPos, Quaternion.identity);
        float pitch       = Random.Range(1 - _PitchChangeMultiplier, 1 + _PitchChangeMultiplier);
        int   randClipIdx = Random.Range(0, clip.Length);

        volume += Random.Range(-_VolumeChangeMultiplier, _VolumeChangeMultiplier);

        audio.clip   = clip[randClipIdx];
        audio.volume = volume;
        audio.pitch  = pitch;

        audio.Play();
    }
}
