using UnityEngine;

[CreateAssetMenu(fileName = "ScenePreviewConfig", menuName = "Resources/ScriptableObject/Scene Preview Config")]
public class ScenePreviewConfig : ScriptableObject {

    /*
     * @brief sceneName is the name of the scene that will be previewed.
     */
    [Header("Scene Name")]
    public string SceneName;

    /*
     * @brief Texture is the render texture that allow us to preview the scene.
     * 
     * @deprecated This attribute is deprecated...
     * Use 'Screenshot' instead.
     */
    [Header("Scene Preview")]
    public RenderTexture Texture;
    public Texture2D     Screenshot;
}
