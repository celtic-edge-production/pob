using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class TrapUtils
{
    public static Vector3 GetMousePosition()
    {
        Camera mainCamera = Camera.main;

        // if camera is desactivated, skip
        if (!mainCamera || mainCamera.isActiveAndEnabled == false)
        {
            return Vector3.zero;
        }
        Vector3 mousePosition = Input.mousePosition;
        mousePosition.z = mainCamera.nearClipPlane + 1;
        mousePosition = mainCamera.ScreenToWorldPoint(mousePosition);

        return mousePosition;
    }
}
