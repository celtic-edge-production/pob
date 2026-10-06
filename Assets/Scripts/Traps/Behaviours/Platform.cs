public class Platform : ATrap
{
    protected override void Awake()
    {
        base.Awake();
        matrix = new int[,] { { 1, 1, 1 } };
    }
}