using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class VolumeControl : MonoBehaviour
{
    [SerializeField] private AudioMixer audioMixer; // Reference to the Audio Mixer
    [SerializeField] private string exposedParameter = "MASTER"; // Exposed parameter in the Audio Mixer
    [SerializeField] private Slider volumeSlider; // Reference to the UI Slider

    private void Start()
    {
        // Ensure the slider reflects the current volume at the start
        if (audioMixer.GetFloat(exposedParameter, out float currentVolume))
        {
            volumeSlider.value = Mathf.InverseLerp(-80f, 20f, currentVolume); // Map volume to slider's range [0, 1]
        }

        // Add listener to update volume whenever the slider changes
        volumeSlider.onValueChanged.AddListener(SetVolume);
    }

    private void OnDestroy()
    {
        // Remove listener when the script is destroyed
        volumeSlider.onValueChanged.RemoveListener(SetVolume);
    }

    public void SetVolume(float sliderValue)
    {
        // Map slider value [0, 1] to Audio Mixer volume range [-80dB, 20dB]
        float newVolume = Mathf.Lerp(-80f, 20f, sliderValue);

        // Set the new volume in the Audio Mixer
        audioMixer.SetFloat(exposedParameter, newVolume);
    }
}
