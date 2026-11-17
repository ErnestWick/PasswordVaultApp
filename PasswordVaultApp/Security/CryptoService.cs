using System;
using System.Security.Cryptography;
using System.Text;

namespace PasswordVaultApp.Security
{
    public static class CryptoService
    {
        // Generates a random 16-byte salt when creating a new user
        public static byte[] GenerateSalt() => RandomNumberGenerator.GetBytes(16);

        // Derives the 32-byte (256-bit) encryption key from the master password
        public static byte[] DeriveMasterKey(string masterPassword, byte[] salt, int iterations = 600_000)
        {
            return Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(masterPassword),
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                32
            );
        }

        // Hashes the derived masterKey so Postgres can verify login without knowing the key
        public static byte[] ComputeAuthHash(byte[] masterKey)
        {
            return SHA256.HashData(masterKey);
        }

        // Encrypts a website password using AES-256-GCM
        public static (byte[] CipherText, byte[] Nonce, byte[] AuthTag) Encrypt(string plainText, byte[] masterKey)
        {
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] cipherText = new byte[plainBytes.Length];
            byte[] nonce = RandomNumberGenerator.GetBytes(12); // 12 bytes recommended for GCM
            byte[] authTag = new byte[16];                     // 16-byte integrity tag

            using var aes = new AesGcm(masterKey, 16);
            aes.Encrypt(nonce, plainBytes, cipherText, authTag);

            return (cipherText, nonce, authTag);
        }

        // Decrypts a website password on demand
        public static string Decrypt(byte[] cipherText, byte[] nonce, byte[] authTag, byte[] masterKey)
        {
            byte[] plainBytes = new byte[cipherText.Length];

            using var aes = new AesGcm(masterKey, 16);
            aes.Decrypt(nonce, cipherText, authTag, plainBytes);

            return Encoding.UTF8.GetString(plainBytes);
        }
    }
}
