using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SoundDestroyer : MonoBehaviour
{
    private AudioSource _AudioSource;

    private void Awake()
    {
        _AudioSource = GetComponent<AudioSource>();
    }

    private IEnumerator Start()
    {
        yield return new WaitForSeconds(_AudioSource.clip.length);
        Destroy(gameObject);
    }
}
