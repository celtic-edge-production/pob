using UnityEngine;

public class Platform: ATrap
{
    override protected void Awake()
    {
        base.Awake();
        matrix = new int[,] {
            { 1, 1, 1 },
        };
    }

    override protected void FixedUpdate()
    {
        bool isCollideActive = (_state & TrapState.Collide) == TrapState.Collide;

        if (isCollideActive)
        {
            PlatformBehaviour();
        }
    }

    private void PlatformBehaviour()
    {
        return;
    }
}
