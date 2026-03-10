using System.ComponentModel.DataAnnotations;

namespace MarriageBureau.Models
{
    /// <summary>
    /// Single-row configuration table for business name and security code.
    /// BusinessName and SecurityCode are stored AES-encrypted in Base64.
    /// </summary>
    public class AppSettings
    {
        [Key]
        public int Id { get; set; } = 1;

        /// <summary>AES-encrypted, Base64-encoded business name</summary>
        public string? EncryptedBusinessName { get; set; }

        /// <summary>AES-encrypted, Base64-encoded security token that embeds expiry date</summary>
        public string? SecurityCode { get; set; }
    }
}
