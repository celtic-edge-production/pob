using System.Text;

// Shared by the Steam and EOS backends so the join-code format stays identical.
public static class RoomCodeGenerator
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private const int Length = 6;

    private static readonly System.Random _random = new System.Random();

    public static string Generate()
    {
        var builder = new StringBuilder(Length);

        for (int i = 0; i < Length; i++)
            builder.Append(Alphabet[_random.Next(Alphabet.Length)]);

        return builder.ToString();
    }
}