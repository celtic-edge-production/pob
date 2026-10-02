using Unity.Netcode;
using UnityEngine;

public class RoundScore : MonoBehaviour
{
    [SerializeField] private Scoreboard _Scoreboard;
    private void OnAnimationCompleted()
    {
        Debug.Log("OnAnimationCompleted !!!!!!!!!!!!!");

        _Scoreboard.OnTextAnimationCompleted();
    }
}
