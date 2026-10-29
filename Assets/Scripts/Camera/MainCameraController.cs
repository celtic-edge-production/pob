using UnityEngine;

public class MainCameraController : MonoBehaviour
{
    [SerializeField] private Vector3 cameraPosition = new Vector3(0, 0, -10);
    [SerializeField] private float orthographicSize = 13.5f;
    [SerializeField] private Vector3 originalPosition = new Vector3(0, 0, -10);
    [SerializeField] private float originalSize;
    [SerializeField] private bool debug = false;

    private void Start()
    {
        originalPosition = Camera.main.transform.position;
        originalSize = Camera.main.orthographicSize;
    }

    public void ResetCamera()
    {
        Camera.main.transform.position = originalPosition;
        Camera.main.orthographicSize = originalSize;
    }

    public void EnlargeCamera()
    {
        Camera.main.transform.position = cameraPosition;
        Camera.main.orthographicSize = orthographicSize;
    }

    private void OnValidate()
    {
        if (Camera.main == null)
            return;

        if (debug)
        {
            // Applies the debug settings
            Camera.main.transform.position = cameraPosition;
            Camera.main.orthographicSize = orthographicSize;
        }
        else if (originalPosition != null)
        {
            // Restores the original settings
            Camera.main.transform.position = originalPosition;
            Camera.main.orthographicSize = originalSize;
        }
    }
}
