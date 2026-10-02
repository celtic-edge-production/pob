using UnityEngine;

namespace Utils {

    public static class RenderTextureToSprite {

        /*
         * @brief Convert a RenderTexture to a Sprite.
         * 
         * @note Know that method will transform the RenderTexture brightness into a darker image.
         */
        public static Sprite Convert(this RenderTexture renderTexture)
        {
            // -- Create a Texture2D with the dimensions of the RenderTexture
            Texture2D texture = new(renderTexture.width, renderTexture.height, TextureFormat.RGB24, false);

            // -- Copy the RenderTexture to the Texture2D
            RenderTexture.active = renderTexture;

            texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            texture.Apply();

            RenderTexture.active = null;

            // -- Create and return a Sprite with the Texture2D
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100.0f);
        }
    }
}
