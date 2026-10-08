using System;

// How a trap is turned when placed: its own art is first mirrored left-right (Mirrored), then
// rotated by quarter turns counterclockwise (QuarterTurns). The transform matches it exactly:
// localScale.x negative when mirrored, rotation z = 90 * QuarterTurns (Unity applies the scale
// before the rotation). Immutable. Plain C#, tested in EditMode (TrapOrientationTests).
public readonly struct TrapOrientation : IEquatable<TrapOrientation>
{
    public static readonly TrapOrientation Default = new TrapOrientation(0, false);

    // 0 to 3, counterclockwise.
    public readonly int QuarterTurns;
    public readonly bool Mirrored;

    public TrapOrientation(int quarterTurns, bool mirrored)
    {
        QuarterTurns = ((quarterTurns % 4) + 4) % 4;
        Mirrored = mirrored;
    }

    public float EulerZ => 90f * QuarterTurns;
    public float ScaleXSign => Mirrored ? -1f : 1f;

    // Positive turns counterclockwise, negative clockwise.
    public TrapOrientation Rotated(int quarterTurns)
    {
        return new TrapOrientation(QuarterTurns + quarterTurns, Mirrored);
    }

    // Flips the trap left-right as seen on screen, whatever its current rotation: a shooter aiming
    // left aims right afterwards. Flipping after a rotation equals mirroring first and rotating the
    // other way, hence the negated turns.
    public TrapOrientation FlippedHorizontally()
    {
        return new TrapOrientation(-QuarterTurns, !Mirrored);
    }

    // The cells covered once oriented, from the shape the trap was designed with.
    public TrapShape Apply(TrapShape baseShape)
    {
        TrapShape shape = Mirrored ? baseShape.MirroredHorizontally() : baseShape;
        return shape.Rotated(QuarterTurns);
    }

    public bool Equals(TrapOrientation other)
    {
        return QuarterTurns == other.QuarterTurns && Mirrored == other.Mirrored;
    }

    public override bool Equals(object obj)
    {
        return obj is TrapOrientation other && Equals(other);
    }

    public override int GetHashCode()
    {
        return QuarterTurns * 2 + (Mirrored ? 1 : 0);
    }

    public override string ToString()
    {
        return $"{QuarterTurns * 90}°{(Mirrored ? " mirrored" : "")}";
    }
}