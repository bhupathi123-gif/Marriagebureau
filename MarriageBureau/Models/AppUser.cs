using System;
using System.ComponentModel.DataAnnotations;

namespace MarriageBureau.Models
{
    /// <summary>Application user with encrypted password for login.</summary>
    public class AppUser
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Username { get; set; } = string.Empty;

        /// <summary>AES-encrypted, Base64-encoded password hash</summary>
        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>Display name shown after login</summary>
        public string? FullName { get; set; }

        /// <summary>Admin = full access; User = read + edit, no settings</summary>
        public string Role { get; set; } = "User";   // Admin | User

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? LastLogin { get; set; }
    }
}
