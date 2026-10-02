public static class MathUtils
{
    public static int SafeDivisor(int count)
    {
        return count > 1 ? count - 1 : 1;
    }
}