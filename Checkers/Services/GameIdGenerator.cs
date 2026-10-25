using System.Security.Cryptography;

namespace Checkers.Services;

public class GameIdGenerator : IGameIdGenerator
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";
    private const int Length = 12;

    public string NextId()
    {
        string id = "";
        for (int i = 0; i < Length; i++)
        {
            id += Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return id;
    }
}
