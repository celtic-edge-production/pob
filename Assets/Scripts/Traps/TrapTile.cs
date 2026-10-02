using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "CustomTiles/TrapTile")]
public class TrapTile : Tile
{
    /*
     * @brief The id of the trap, it must be unique
     */
    public int id = 0;

    /*
     * @brief The prefab of the trap, must be removed from the list of traps and delete when destroyed
     */
    public ATrap trapPrefab;

    public Sprite testSprite; // Reference to the sprite used for testing

    private void OnEnable()
    {
        // Set the sprite property of the base Tile class
        this.sprite = testSprite;
    }
}
