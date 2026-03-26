using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace MarriageBureau.Services
{
    /// <summary>
    /// AES-256 encryption / decryption service shared by both the main app and the admin tool.
    /// Key is derived from a hard-coded passphrase + salt via PBKDF2 so it never changes.
    /// </summary>
    public static class CryptoService
    {
        // ── Shared secret (same in main app AND admin tool) ──────────────────
        // IMPORTANT: Do NOT change these after deployment – existing encrypted data will break.
        private const string Passphrase = "MB$2025#SecureKey!@MarriageBureau";
        private const string Salt       = "MB_Salt_2025_v1";

        private static readonly byte[] KeyBytes;
        private static readonly byte[] IvBytes;

        static CryptoService()
        {
            // Derive a stable 256-bit key and 128-bit IV from the passphrase
            using var kdf = new Rfc2898DeriveBytes(
                Passphrase,
                Encoding.UTF8.GetBytes(Salt),
                100_000,
                HashAlgorithmName.SHA256);

            KeyBytes = kdf.GetBytes(32);   // AES-256
            IvBytes  = kdf.GetBytes(16);   // AES block size
        }

        // ── Encryption ────────────────────────────────────────────────────────

        /// <summary>Encrypts plain-text and returns a Base64-encoded cipher string.</summary>
        public static string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return string.Empty;

            using var aes = Aes.Create();
            aes.Key  = KeyBytes;
            aes.IV   = IvBytes;
            aes.Mode = CipherMode.CBC;

            using var encryptor = aes.CreateEncryptor();
            var inputBytes  = Encoding.UTF8.GetBytes(plainText);
            var cipherBytes = encryptor.TransformFinalBlock(inputBytes, 0, inputBytes.Length);
            return Convert.ToBase64String(cipherBytes);
        }

        /// <summary>Decrypts a Base64-encoded cipher string back to plain-text.</summary>
        public static string Decrypt(string cipherBase64)
        {
            if (string.IsNullOrEmpty(cipherBase64)) return string.Empty;
            try
            {
                using var aes = Aes.Create();
                aes.Key  = KeyBytes;
                aes.IV   = IvBytes;
                aes.Mode = CipherMode.CBC;

                using var decryptor = aes.CreateDecryptor();
                var cipherBytes  = Convert.FromBase64String(cipherBase64);
                var plainBytes   = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch
            {
                return string.Empty;
            }
        }

        // ── Password Hashing ──────────────────────────────────────────────────

        /// <summary>
        /// Derives a one-way hash for a password using PBKDF2-SHA256.
        /// Returns "HASH:SALT" (both Base64-encoded) for storage.
        /// </summary>
        public static string HashPassword(string password)
        {
            var saltBytes = RandomNumberGenerator.GetBytes(16);
            using var kdf = new Rfc2898DeriveBytes(password, saltBytes, 100_000, HashAlgorithmName.SHA256);
            var hash = kdf.GetBytes(32);
            return $"{Convert.ToBase64String(hash)}:{Convert.ToBase64String(saltBytes)}";
        }

        /// <summary>Verifies a password against a "HASH:SALT" stored value.</summary>
        public static bool VerifyPassword(string password, string storedHash)
        {
            try
            {
                var parts = storedHash.Split(':');
                if (parts.Length != 2) return false;
                var hash      = Convert.FromBase64String(parts[0]);
                var saltBytes = Convert.FromBase64String(parts[1]);
                using var kdf = new Rfc2898DeriveBytes(password, saltBytes, 100_000, HashAlgorithmName.SHA256);
                var computed  = kdf.GetBytes(32);
                return CryptographicOperations.FixedTimeEquals(hash, computed);
            }
            catch { return false; }
        }

        // ── Security Code (business licence) ─────────────────────────────────

        /// <summary>
        /// Creates a security code that embeds businessName and expiryDate.
        /// Format (before encryption): "MB|{businessName}|{expiryDate:yyyyMMdd}"
        /// </summary>
        public static string GenerateSecurityCode(string businessName, DateTime expiryDate)
        {
            var payload = $"MB|{businessName}|{expiryDate:yyyyMMdd}";
            return Encrypt(payload);
        }

        /// <summary>
        /// Validates a security code.  Returns (isValid, businessName, expiryDate).
        /// isValid = false if the code is corrupt, expired, or format is wrong.
        /// </summary>
        public static (bool IsValid, string BusinessName, DateTime ExpiryDate, string Message)
            ValidateSecurityCode(string securityCode)
        {
            var plain = Decrypt(securityCode);
            if (string.IsNullOrWhiteSpace(plain))
                return (false, "", DateTime.MinValue, "Invalid security code – decryption failed.");

            var parts = plain.Split('|');
            if (parts.Length != 3 || parts[0] != "MB")
                return (false, "", DateTime.MinValue, "Invalid security code – wrong format.");

            var businessName = parts[1];
            if (!DateTime.TryParseExact(parts[2], "yyyyMMdd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var expiry))
                return (false, businessName, DateTime.MinValue, "Invalid security code – bad expiry date.");

            if (DateTime.Today > expiry)
                return (false, businessName, expiry,
                    $"Licence expired on {expiry:dd-MMM-yyyy}. Please contact the administrator to renew.");

            var daysLeft = (expiry - DateTime.Today).Days;
            var msg = daysLeft <=2
                ? $"Licence expires in {daysLeft} day(s) on {expiry:dd-MMM-yyyy}."
                : ""; //$"Licence valid until {expiry:dd-MMM-yyyy}.";

            return (true, businessName, expiry, msg);
        }
    }
}
