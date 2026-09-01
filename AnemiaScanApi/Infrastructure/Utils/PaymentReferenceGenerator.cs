using System.Numerics;
using System.Security.Cryptography;

namespace AnemiaScanApi.Utils;

/// <summary>
/// Generates Solana Pay reference keys: 32 random bytes rendered as base58, the same shape as a
/// Solana public key. The key never signs anything — it only rides along in the transaction so
/// we can find it later via <c>getSignaturesForAddress</c>.
/// </summary>
public static class PaymentReferenceGenerator
{
    private const string Base58Alphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
    private const int KeyLengthBytes = 32;

    /// <summary>
    /// Returns a fresh base58-encoded 32-byte reference key.
    /// </summary>
    public static string NewReference() => ToBase58(RandomNumberGenerator.GetBytes(KeyLengthBytes));

    /// <summary>
    /// Encodes bytes as base58 using the Bitcoin/Solana alphabet, preserving leading zero bytes as '1'.
    /// </summary>
    public static string ToBase58(byte[] bytes)
    {
        if (bytes.Length == 0) return string.Empty;

        // BigInteger wants little-endian and a trailing zero byte to stay unsigned.
        var value = new BigInteger(bytes.Reverse().Append((byte)0).ToArray());

        var chars = new Stack<char>();
        while (value > 0)
        {
            value = BigInteger.DivRem(value, 58, out var remainder);
            chars.Push(Base58Alphabet[(int)remainder]);
        }

        var leadingZeros = bytes.TakeWhile(b => b == 0).Count();
        return new string(Base58Alphabet[0], leadingZeros) + new string(chars.ToArray());
    }
}
