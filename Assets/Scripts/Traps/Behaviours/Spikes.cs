using UnityEngine;

public class Spikes : ATrap
{
    protected override void Awake()
    {
        base.Awake();
        matrix = new int[,] { { 1 } };
    }

    // The effect on the player is handled by PlayerCharacter; the trap only plays its sound.
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (IsServer && IsActive && collision.CompareTag("Player"))
            PlayActionSound();
    }
}