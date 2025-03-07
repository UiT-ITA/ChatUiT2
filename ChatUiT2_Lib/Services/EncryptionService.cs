using ChatUiT2.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace ChatUiT2.Services;


/// <summary>
/// Service for encryption and decryption.
/// USed to encrypt user chat data stored in mongodb
/// </summary>
public class EncryptionService : IEncryptionService
{

    /// <summary>
    /// Encrypts string data using AES256 with supplied key
    /// </summary>
    /// <param name="data">Data to encrypt</param>
    /// <param name="key">The key to use</param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public byte[] Encrypt(string data, byte[] key)
    {
        return Encrypt(Encoding.UTF8.GetBytes(data), key);
    }

    public byte[] Encrypt(byte[] data, byte[] key)
    {
        if (key.Length == 0) throw new Exception("aesKey is empty");

        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV(); // Generate a new IV
        byte[] iv = aes.IV; // Get the generated IV

        using var encryptor = aes.CreateEncryptor(key, iv);
        using var ms = new MemoryStream();
        ms.Write(iv, 0, iv.Length); // Prepend the IV to the data
        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        {
            cs.Write(data, 0, data.Length);
        }
        return ms.ToArray();
    }

    public string DecryptString(byte[] encryptedData, byte[] key)
    {
        //return Decrypt(Encoding.UTF8.GetBytes(encryptedData), key);
        return Encoding.UTF8.GetString(Decrypt(encryptedData, key));
    }

    /// <summary>
    /// Decrypts string data using AES256 with supplied key
    /// </summary>
    /// <param name="encryptedData">The data to decrypt</param>
    /// <param name="key">The key to use</param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public byte[] Decrypt(byte[] encryptedData, byte[] key)
    {
        if (key.Length == 0) throw new Exception("aesKey is empty");
        byte[] data;

        using (var aes = Aes.Create())
        {
            aes.Key = key;
            // Extract the IV from the beginning of the encrypted data
            byte[] iv = new byte[aes.BlockSize / 8];
            Array.Copy(encryptedData, iv, iv.Length);
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor(key, iv);
            using var ms = new MemoryStream(encryptedData);
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var reader = new BinaryReader(cs);
            ms.Seek(iv.Length, SeekOrigin.Begin); // Skip past the IV
            data = reader.ReadBytes(encryptedData.Length - iv.Length);
        }
        return data;
    }

    /// <summary>
    /// Create secure encryption key for AES256
    /// </summary>
    /// <param name="password">Password bytes</param>
    /// <param name="salt">Salt bytes</param>
    /// <param name="iterations">Nr of iterations. Makes brute force more difficult</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public byte[] GetEncryptionKeyForAes256(byte[] password, byte[] salt, int iterations)
    {
        if (iterations < 50000)
        {
            throw new ArgumentException("Iterations must be at least 50000 to be secure", "iterations");
        }
        if (salt == null)
        {
            throw new ArgumentException("Salt can not be null", "salt");
        }
        if (salt.Count(b => b > 0) == 0)
        {
            throw new ArgumentException("Salt must have at least one non zero byte", "salt");
        }
        if (salt.Length < 16)
        {
            throw new ArgumentException("Salt must be at least 16 bytes", "salt");
        }
        if (password.Length < 32)
        {
            throw new ArgumentException("Password must to at least 32 bytes", "password");
        }
        if (password.Where(b => b > 0).ToList().Count() == 0)
        {
            throw new ArgumentException("Password must contain at least one none zero byte", "password");
        }


        using (Rfc2898DeriveBytes rfc2898 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256))
        {
            byte[] key = rfc2898.GetBytes(32);
            return key;
        }
    }

    /// <summary>
    /// Create a random byte array using a secure random source.
    /// 
    /// </summary>
    /// <param name="length">Nr of bytes in result array</param>
    /// <returns></returns>
    public byte[] GetRandomByteArray(int length)
    {
        byte[] bytes = new byte[length];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        return bytes;
    }
}
