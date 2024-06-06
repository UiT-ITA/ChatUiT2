
namespace ChatUiT2.Interfaces;

public interface IEncryptionService
{
    byte[] Encrypt(string data, byte[] key);
    string Decrypt(byte[] encryptedData, byte[] key);
    byte[] GetEncryptionKeyForAes256(byte[] password, byte[] salt, int iterations);
    byte[] GetRandomByteArray(int length);
}
