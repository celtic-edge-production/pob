using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Utils {

    public static class ScenePreviewLoader {

        /*
         * @brief Load a scene preview.
         * That means that we can see the scene, but we can't interact with it, and the scripts are not running.
         * 
         * The translation is the position where the scene will be loaded.
         * 
         * @deprecated This method is deprecated...
         */
        public static void LoadScenePreview(string sceneName, Vector3 translation = default)
        {
            // -- Load the scene in additive mode (meaning that the current scene will not be unloaded)
            SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);

            // -- Get the scene
            Scene scene = SceneManager.GetSceneByName(sceneName);

            // -- Get all the game objects in the scene and disable them
            foreach (GameObject obj in scene.GetRootGameObjects()) {
                MoveObject(obj, translation);
                DisabledScriptOfObject(obj);
            }
        }

        /*
         * @brief Move every game object of the scene by the translation.
         */
        private static void MoveObject(GameObject obj, Vector3 translation)
        {
            obj.transform.position += translation;
        }

        /*
         * @brief Disable the MonoBehaviour scripts of the object and its children.
         */
        private static void DisabledScriptOfObject(GameObject obj)
        {
            // -- Disable the scripts of the object
            foreach (MonoBehaviour script in obj.GetComponents<MonoBehaviour>())
                script.enabled = false;

            // -- Disable the scripts of the children
            foreach (Transform child in obj.transform)
                DisabledScriptOfObject(child.gameObject);
        }
    }
}
