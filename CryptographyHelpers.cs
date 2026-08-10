using System.Security.Cryptography;
using System.Text;

namespace Chat;

public static class CryptographyHelpers
{
    public static string ProtectWithDpapi(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext)) return string.Empty;
        byte[] bytes = Encoding.UTF8.GetBytes(plaintext);
        byte[] encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encrypted);
    }

    public static string UnprotectWithDpapi(string ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext)) return string.Empty;
        try
        {
            byte[] bytes = Convert.FromBase64String(ciphertext);
            byte[] decrypted = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }
        catch
        {
            return string.Empty;
        }
    }
    
    // Type1 PBKDF2/AES-GCM format encryption/decryption
    // We will use a simple format: 
    // [16 bytes salt] + [12 bytes nonce] + [16 bytes tag] + [ciphertext]
    public static string ProtectWithType1(string plaintext, string password)
    {
        if (string.IsNullOrEmpty(plaintext) || string.IsNullOrEmpty(password)) return string.Empty;
        
        byte[] salt = new byte[16];
        byte[] nonce = new byte[12];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(salt);
            rng.GetBytes(nonce);
        }
        
        byte[] key = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, 100000, HashAlgorithmName.SHA256, 32);
        
        byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        byte[] ciphertext = new byte[plaintextBytes.Length];
        byte[] tag = new byte[16];
        
        using (var aesGcm = new AesGcm(key, 16))
        {
            aesGcm.Encrypt(nonce, plaintextBytes, ciphertext, tag);
        }
        
        byte[] result = new byte[salt.Length + nonce.Length + tag.Length + ciphertext.Length];
        Buffer.BlockCopy(salt, 0, result, 0, salt.Length);
        Buffer.BlockCopy(nonce, 0, result, salt.Length, nonce.Length);
        Buffer.BlockCopy(tag, 0, result, salt.Length + nonce.Length, tag.Length);
        Buffer.BlockCopy(ciphertext, 0, result, salt.Length + nonce.Length + tag.Length, ciphertext.Length);
        
        return Convert.ToBase64String(result);
    }
    
    public static string UnprotectWithType1(string base64Ciphertext, string password)
    {
        if (string.IsNullOrEmpty(base64Ciphertext) || string.IsNullOrEmpty(password)) return string.Empty;
        try
        {
            byte[] data = Convert.FromBase64String(base64Ciphertext);
            if (data.Length < 16 + 12 + 16) return string.Empty;
            
            byte[] salt = new byte[16];
            byte[] nonce = new byte[12];
            byte[] tag = new byte[16];
            byte[] ciphertext = new byte[data.Length - (16 + 12 + 16)];
            
            Buffer.BlockCopy(data, 0, salt, 0, salt.Length);
            Buffer.BlockCopy(data, salt.Length, nonce, 0, nonce.Length);
            Buffer.BlockCopy(data, salt.Length + nonce.Length, tag, 0, tag.Length);
            Buffer.BlockCopy(data, salt.Length + nonce.Length + tag.Length, ciphertext, 0, ciphertext.Length);
            
            byte[] key = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, 100000, HashAlgorithmName.SHA256, 32);
            byte[] plaintext = new byte[ciphertext.Length];
            
            using (var aesGcm = new AesGcm(key, 16))
            {
                aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);
            }
            
            return Encoding.UTF8.GetString(plaintext);
        }
        catch
        {
            return string.Empty;
        }
    }
}
