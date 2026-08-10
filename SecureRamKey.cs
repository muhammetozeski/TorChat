using System.Security.Cryptography;

namespace Chat;

/// <summary>
/// Keeps a byte array securely encrypted in RAM using ProtectedMemory.
/// It decrypts only when accessed and must be properly disposed.
/// </summary>
public class SecureRamKey : IDisposable
{
    private byte[]? _encryptedData;
    private readonly int _originalLength;

    public SecureRamKey(byte[] secret)
    {
        _originalLength = secret.Length;
        // ProtectedData.Protect handles arbitrary lengths and paddings securely.
        _encryptedData = ProtectedData.Protect(secret, null, DataProtectionScope.CurrentUser);
    }

    /// <summary>
    /// Decrypts the memory momentarily and passes it to the action,
    /// then immediately zeroes out the decrypted buffer.
    /// </summary>
    public void Use(Action<byte[]> action)
    {
        if (_encryptedData == null) throw new ObjectDisposedException(nameof(SecureRamKey));

        byte[]? decrypted = null;
        try
        {
            decrypted = ProtectedData.Unprotect(_encryptedData, null, DataProtectionScope.CurrentUser);
            action(decrypted);
        }
        finally
        {
            if (decrypted != null)
            {
                Array.Clear(decrypted, 0, decrypted.Length);
            }
        }
    }
    
    /// <summary>
    /// Gets a base64 representation of the decrypted memory (for Tor control port).
    /// </summary>
    public string GetBase64()
    {
        string base64 = string.Empty;
        Use(key => { base64 = Convert.ToBase64String(key); });
        return base64;
    }

    public void Dispose()
    {
        if (_encryptedData != null)
        {
            Array.Clear(_encryptedData, 0, _encryptedData.Length);
            _encryptedData = null;
        }
    }
}
