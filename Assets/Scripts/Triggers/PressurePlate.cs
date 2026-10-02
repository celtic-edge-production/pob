using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(Animator))]
public class PressurePlate : MonoBehaviour {

    #region Attributes

    /*
     * @brief OnPressed is an event called when the pressure plate is pressed.
     * It can be any event that needs to be triggered when the pressure plate is pressed.
     * The only parameter for this event is a boolean that indicates if the pressure plate is pressed or not.
     * - True : The pressure plate is pressed.
     * - False: The pressure plate is not pressed.
     */
    public event Action<GameObject, bool> OnPressed;

    /*
     * @brief _AllowTag is a list of tags that are allowed to press the pressure plate.
     * In the editor, you can add tags that are allowed to press the pressure plate.
     * For example, if you want the player to press the pressure plate, but not the enemies, you can add the player tag to the list.
     */
    [SerializeField]
    private List<string> _AllowTag = new();

    /*
     * @brief _Animator is the animator of the pressure plate.
     * It allow the pressure plate to have an animation when it is pressed.
     * And switch back to the original state when it is not pressed.
     */
    [SerializeField]
    private Animator _Animator;

    #endregion

    #region Methods

    public void OnEnable()
    {
        _Animator = GetComponent<Animator>(); // -- Get the animator component.
    }

    public void OnDisable()
    {
        _Animator = null; // -- Set the animator to null.
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // -- Check if the tag is allowed to press the pressure plate.
        if (IsTagAllowed(collision.gameObject.tag))
            OnPressed?.Invoke(collision.gameObject, true);
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject == null)
            return;
        GameObject obj = collision.gameObject;

        // -- Check if the tag is allowed to press the pressure plate.
        if (IsTagAllowed(obj.tag))
            OnPressed?.Invoke(obj, false);
    }

    public void UpdatePlateState(bool isPressed)
    {
        _Animator.SetBool("OnTriggered", isPressed);
    }

    #endregion

    #region Getters

    /*
     * @brief IsTagAllowed is a method that checks if the tag is allowed to press the pressure plate.
     * - True : The tag is allowed to press the pressure plate.
     * - False: The tag is not allowed to press the pressure plate.
     */
    private bool IsTagAllowed(string tag)
    {
        return _AllowTag.Contains(tag);
    }

    #endregion
}
