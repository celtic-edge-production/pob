using UnityEngine;

public class Spikes : ATrap
{
    protected override void Awake()
    {
        base.Awake();
        matrix = new int[,] { { 1 } };
    }

    public override void OnPlayerHit()
    {
        if (IsActive)
            PlayActionSound();
    }
}