using Unity.Netcode;
using UnityEngine;

// The chest's UI, where the offered traps appear. Only plays its animations: the sequence is run
// by TrapSelection, which reads HasOpened / HasClosed (set by the animation events).
public class ChestUi : NetworkBehaviour
{
    private Animator _Animator;

    // Server only.
    public bool HasOpened { get; private set; }
    public bool HasClosed { get; private set; }

    private void Awake()
    {
        _Animator = GetComponent<Animator>();
        _Animator.SetBool("IsOpening", false);
        _Animator.SetBool("IsToggle", false);
    }

    // Server only. The UI starts inactive: activating it runs Awake first.
    public void PlayOpen()
    {
        HasOpened = false;
        gameObject.SetActive(true);
        _Animator.SetBool("IsToggle", true);
        _Animator.SetBool("IsOpening", true);
    }

    // Server only.
    public void PlayClose()
    {
        HasClosed = false;
        _Animator.SetBool("IsOpening", false);
        _Animator.SetBool("IsToggle", true);
    }

    // Server only: back to rest once the closing animation is over.
    public void EndClose()
    {
        _Animator.SetBool("IsToggle", false);
    }

    // Animation event: end of the opening animation.
    public void OnUIOpen()
    {
        if (IsServer)
            HasOpened = true;
    }

    // Animation event: end of the closing animation.
    public void OnUIClose()
    {
        if (IsServer)
            HasClosed = true;
    }
}