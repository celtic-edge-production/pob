using UnityEngine;

// -- Please use this function only in the lobby scene -- //

/*
 * @brief Lobby Character is a script that handles some characters controls only in the lobby scene.
 */
public class LobbyCharacter : MonoBehaviour {

    [Header("Spawn Emplacement (Lobby)")]
    [SerializeField]
    private Vector3 _OriginalPosition = Vector3.zero;

    private void Awake()
    {
        _OriginalPosition = transform.position;
    }

    /*
     * @brief Call this function when you want to reset the character to its original position.
     * @note Original position is given in the lobby (it represents the spawn emplacement of the character).
     */
    public void ResetToOriginalPosition()
    {
        transform.position = _OriginalPosition;
    }
}
