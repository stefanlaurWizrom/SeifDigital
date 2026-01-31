using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace SeifDigital.Services
{
    public class EncryptionService
    {
        private readonly byte[] _key;

        public EncryptionService(IOptions<CryptoOptions> options)
        {
            var keyBase64 = options.Value.MasterKeyBase64;

            if (string.IsNullOrWhiteSpace(keyBase64))
                throw new InvalidOperationException("Crypto:MasterKeyBase64 nu este setat în appsettings.json");

            _key = Convert.FromBase64String(keyBase64);

            if (_key.Length != 32)
                throw new InvalidOperationException("Crypto:MasterKeyBase64 trebuie să fie 32 bytes (Base64).");
        }

        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return "";

            using var aes = Aes.Create();
            aes.Key = _key;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            aes.GenerateIV();
            var iv = aes.IV;

            using var encryptor = aes.CreateEncryptor(aes.Key, iv);
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

            // stocăm: IV + CIPHER
            var combined = new byte[iv.Length + cipherBytes.Length];
            Buffer.BlockCopy(iv, 0, combined, 0, iv.Length);
            Buffer.BlockCopy(cipherBytes, 0, combined, iv.Length, cipherBytes.Length);

            return Convert.ToBase64String(combined);
        }

        public string Decrypt(string cipherTextBase64)
        {
            if (string.IsNullOrEmpty(cipherTextBase64)) return "";

            var combined = Convert.FromBase64String(cipherTextBase64);

            using var aes = Aes.Create();
            aes.Key = _key;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            var iv = new byte[16];
            if (combined.Length < iv.Length)
                throw new CryptographicException("Ciphertext invalid (prea scurt).");

            Buffer.BlockCopy(combined, 0, iv, 0, iv.Length);

            var cipherBytes = new byte[combined.Length - iv.Length];
            Buffer.BlockCopy(combined, iv.Length, cipherBytes, 0, cipherBytes.Length);

            using var decryptor = aes.CreateDecryptor(aes.Key, iv);
            var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

            return Encoding.UTF8.GetString(plainBytes);
        }
    }
}
