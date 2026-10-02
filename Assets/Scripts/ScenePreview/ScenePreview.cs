using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScenePreview : MonoBehaviour {

    #region Attributes

    /*
     * @brief OnLevelSelected is an event called when the level is selected.
     * (here, when the children pressure plate is pressed).
     */
    public event Action<GameObject, string> OnLevelSelected;

    /*
     * @brief OnLevelDeSelected is an event called when the level is deselected.
     * (here, when the children pressure plate is not pressed).
     */
    public event Action<GameObject, string> OnLevelDeSelected;

    /*
     * @brief Config is the configuration of the scene to preview.
     */
    [Header("Config of the Scene to preview")]
    [SerializeField]
    private ScenePreviewConfig _Config;

    [Header("Children to enable")]
    [SerializeField]
    private LevelConverter _LevelConverter;

    [SerializeField]
    private NetworkPressurePlate _PressurePlate;

    #endregion

    #region Methods

    public void Awake()
    {
        // -- Enabled Level preview
        _LevelConverter.gameObject.SetActive(true);
        _LevelConverter.Convert(_Config.Screenshot);

        // -- Enabled Pressure Plate
        _PressurePlate.gameObject.SetActive(true);
        _PressurePlate.OnPressed += Event_OnPressed;
    }

    private void Event_OnPressed(GameObject obj, bool isPressed)
    {
        if (isPressed)
            OnLevelSelected?.Invoke(obj, _Config.SceneName);
        else
            OnLevelDeSelected?.Invoke(obj, _Config.SceneName);
    }

    public void Unbind()
    {
        _PressurePlate.OnPressed -= Event_OnPressed;
    }

    #endregion

    #region Methods - @Deprecated

    /*public void Awake()
    {
        // -- Load Preview Scene
        Utils.ScenePreviewLoader.LoadScenePreview(_Config.SceneName, new Vector3(100, 100, 0)); // Deprecated code....

        // -- Enabled Level preview
        _LevelConverter.gameObject.SetActive(true);
        _LevelConverter.Convert(_Config.Texture); // Deprecated code....
    }*/

    #endregion
}
