using System.Security.Cryptography;

namespace Campsite.Services;
public sealed class PasswordService
{
    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 210000, HashAlgorithmName.SHA512, 32);
        return $"pbkdf2-sha512$210000${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }
    public bool Verify(string password, string stored)
    {
        var parts = stored.Split('$'); if (parts.Length != 4 || parts[0] != "pbkdf2-sha512") return false;
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, Convert.FromBase64String(parts[2]), int.Parse(parts[1]), HashAlgorithmName.SHA512, 32);
        return CryptographicOperations.FixedTimeEquals(hash, Convert.FromBase64String(parts[3]));
    }
}
