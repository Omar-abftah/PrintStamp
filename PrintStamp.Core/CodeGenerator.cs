using System.Security.Cryptography;
namespace PrintStamp.Core;

public class CodeGenerator
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static string New(int length = 10) =>
        string.Create(length, 0, (span, _) =>
        {
            for (int i = 0; i < span.Length; i++)
                span[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        });

    // scanners can add whitespace or lowercase, so normalize before lookup
    public static string Normalize(string raw) => raw.Trim().ToUpperInvariant();
}