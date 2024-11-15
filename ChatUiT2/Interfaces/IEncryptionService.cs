
namespace ChatUiT2.Interfaces;

public interface IEncryptionService
{
    byte[] Encrypt(byte[] data, byte[] key);
    byte[] Encrypt(string data, byte[] key);
    byte[] Decrypt(byte[] encryptedData, byte[] key);
    string DecryptString(byte[] encryptedData, byte[] key);
    byte[] GetEncryptionKeyForAes256(byte[] password, byte[] salt, int iterations);
    byte[] GetRandomByteArray(int length);
}
