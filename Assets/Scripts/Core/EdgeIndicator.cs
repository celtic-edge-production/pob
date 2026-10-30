using System;

// Where an arrow sits on the screen edge to point at something off screen. Works in viewport
// coordinates (0..1 on both axes, the screen centre at 0.5). Pure, tested in EditMode;
// OffscreenIndicators applies it.
public static class EdgeIndicator
{
    // Inside the screen, margin excluded (a target right at the edge still gets its arrow).
    public static bool IsOnScreen(float viewportX, float viewportY, float margin)
    {
        return viewportX >= margin && viewportX <= 1f - margin
            && viewportY >= margin && viewportY <= 1f - margin;
    }

    // The point where the line from the centre to the target crosses the edge (margin included),
    // and the arrow's angle in degrees (0 = right, 90 = up). A target behind the camera has its
    // direction flipped: perspective projections mirror it.
    public static void PlaceOnEdge(float viewportX, float viewportY, bool isBehind, float margin,
        out float edgeX, out float edgeY, out float angleDegrees)
    {
        float dx = viewportX - 0.5f;
        float dy = viewportY - 0.5f;

        if (isBehind)
        {
            dx = -dx;
            dy = -dy;
        }

        // Right on the centre (should not happen off screen): point up.
        if (Math.Abs(dx) < 1e-6f && Math.Abs(dy) < 1e-6f)
            dy = 1f;

        float half = 0.5f - margin;
        float scale = Math.Min(
            Math.Abs(dx) > 1e-6f ? half / Math.Abs(dx) : float.MaxValue,
            Math.Abs(dy) > 1e-6f ? half / Math.Abs(dy) : float.MaxValue);

        edgeX = 0.5f + dx * scale;
        edgeY = 0.5f + dy * scale;
        angleDegrees = (float)(Math.Atan2(dy, dx) * 180.0 / Math.PI);
    }
}
