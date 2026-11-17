using System;

namespace PasswordVaultApp.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public byte[] AuthHash { get; set; } = Array.Empty<byte>();
        public byte[] KdfSalt { get; set; } = Array.Empty<byte>();
        public int KdfIterations { get; set; } = 600_000;
    }

    public class VaultItem
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int? CategoryId { get; set; }
        public string SiteName { get; set; } = string.Empty;
        public string? SiteUrl { get; set; }
        public string AccountUsername { get; set; } = string.Empty;
        public byte[] EncryptedPassword { get; set; } = Array.Empty<byte>();
        public byte[] Nonce { get; set; } = Array.Empty<byte>();
        public byte[] AuthTag { get; set; } = Array.Empty<byte>();
        public bool IsFavorite { get; set; }
    }
}
