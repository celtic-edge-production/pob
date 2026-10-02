using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class LevelConverter : MonoBehaviour {

    #region Attributes

    /*
     * @brief The sprite renderer,
     * that will contain after the convertion of the render texture the preview of the scene.     
     */
    private SpriteRenderer _Sprite;

    #endregion

    #region Methods

    public void OnEnable()
    {
        _Sprite = GetComponent<SpriteRenderer>();
    }

    public void OnDisable()
    {
        _Sprite = null;
    }

    /*
     * @brief Convert the render texture to a sprite.
     * 
     * @deprecated This method is deprecated...
     * Use the Convert(Texture2D) method instead.
     */
    public void Convert(RenderTexture renderTexture)
    {
        _Sprite.sprite = Utils.RenderTextureToSprite.Convert(renderTexture);
    }

    public void Convert(Texture2D texture2D)
    {
        _Sprite.sprite = Sprite.Create(texture2D, new Rect(0, 0, texture2D.width, texture2D.height), new Vector2(0.5f, 0.5f), 100.0f);
    }

    #endregion
}
