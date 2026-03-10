using System;
using MarriageBureau.Data;
using MarriageBureau.Models;

namespace MarriageBureau.Services
{
    /// <summary>
    /// Reads AppSettings from DB and validates the security code at startup.
    /// Exposes the decrypted business name for use in exports and UI.
    /// </summary>
    public static class LicenceService
    {
        private static string _businessName = "Marriage Bureau";
        private static bool   _licenceValid = false;
        private static string _licenceMessage = "Licence not validated.";
        private static DateTime _expiryDate = DateTime.MinValue;

        public static string BusinessName  => _businessName;
        public static bool   IsValid       => _licenceValid;
        public static string Message       => _licenceMessage;
        public static DateTime ExpiryDate  => _expiryDate;

        /// <summary>
        /// Call once at application startup.
        /// Loads settings from DB, decrypts business name, validates security code.
        /// Returns (isValid, message).
        /// </summary>
        public static (bool IsValid, string Message) Validate()
        {
            try
            {
                using var ctx = new AppDbContext();
                var settings = ctx.AppSettings.Find(1);

                if (settings == null)
                {
                    _licenceValid   = false;
                    _licenceMessage = "No licence configuration found. Please contact the administrator.";
                    return (_licenceValid, _licenceMessage);
                }

                // Decrypt business name
                if (!string.IsNullOrWhiteSpace(settings.EncryptedBusinessName))
                {
                    var name = CryptoService.Decrypt(settings.EncryptedBusinessName);
                    if (!string.IsNullOrWhiteSpace(name))
                        _businessName = name;
                }

                // Validate security code
                if (string.IsNullOrWhiteSpace(settings.SecurityCode))
                {
                    _licenceValid   = false;
                    _licenceMessage = "No security code configured. Please contact the administrator to activate the application.";
                    return (_licenceValid, _licenceMessage);
                }

                var (valid, bizName, expiry, msg) =
                    CryptoService.ValidateSecurityCode(settings.SecurityCode);

                _licenceValid   = valid;
                _expiryDate     = expiry;
                _licenceMessage = msg;

                if (!string.IsNullOrWhiteSpace(bizName))
                    _businessName = bizName;

                return (_licenceValid, _licenceMessage);
            }
            catch (Exception ex)
            {
                _licenceValid   = false;
                _licenceMessage = $"Licence check failed: {ex.Message}";
                return (_licenceValid, _licenceMessage);
            }
        }

        /// <summary>Reload from DB (called after settings are updated).</summary>
        public static void Reload() => Validate();
    }
}
