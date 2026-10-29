using UnityEngine;
using UnityEngine.InputSystem;

public static class TrapUtils
{
    public static Vector3 GetMousePosition()
    {
        Camera mainCamera = Camera.main;

        // if camera is desactivated, skip
        if (!mainCamera || mainCamera.isActiveAndEnabled == false || Mouse.current == null)
        {
            return Vector3.zero;
        }
        Vector3 mousePosition = Mouse.current.position.ReadValue();
        mousePosition.z = mainCamera.nearClipPlane + 1;
        mousePosition = mainCamera.ScreenToWorldPoint(mousePosition);

        return mousePosition;
    }
}
