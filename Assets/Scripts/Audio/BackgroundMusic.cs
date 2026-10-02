using UnityEngine;

public class BackgroundMusic : MonoBehaviour
{
    [SerializeField] private AudioClip _BackgroundMusic;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (_BackgroundMusic != null)
        {
            // Add an AudioSource component dynamically if it's not already attached
            AudioSource audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            // Configure the AudioSource to loop the background music
            audioSource.clip = _BackgroundMusic;
            audioSource.loop = true;
            audioSource.playOnAwake = true;

            // Play the background music
            audioSource.Play();
        }
        else
        {
            Debug.LogWarning("No background music assigned.");
        }
    }
}
