using Microsoft.VisualBasic;
using Npgsql;
using PasswordVaultApp.Models;
using PasswordVaultApp.Security;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows;

namespace PasswordVaultApp.Data
{
    public class VaultRepository
    {
        // Replace password with the one you set for vault_app_user
        private readonly string _connString =
            "Host=localhost;Port=5432;Database=password_vault;Username=vault_app_user;Password=TuPasswordSeguraAqui123!";

        // 1. Register a new master account
        public async Task<bool> RegisterUserAsync(string email, string masterPassword)
        {
            byte[] salt = CryptoService.GenerateSalt();
            int iterations = 600_000;
            byte[] masterKey = CryptoService.DeriveMasterKey(masterPassword, salt, iterations);
            byte[] authHash = CryptoService.ComputeAuthHash(masterKey);

            await using var conn = new NpgsqlConnection(_connString);
            await conn.OpenAsync();

            const string sql = @"INSERT INTO users (email, auth_hash, kdf_salt, kdf_iterations) 
                                 VALUES (@email, @hash, @salt, @iter)";

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("email", email);
            cmd.Parameters.AddWithValue("hash", authHash);
            cmd.Parameters.AddWithValue("salt", salt);
            cmd.Parameters.AddWithValue("iter", iterations);

            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        // 2. Unlock vault (returns User + derived masterKey in RAM if valid)
        public async Task<(User? User, byte[]? MasterKey)> LoginAsync(string email, string masterPassword)
        {
            await using var conn = new NpgsqlConnection(_connString);
            await conn.OpenAsync();

            const string sql = "SELECT id, email, auth_hash, kdf_salt, kdf_iterations FROM users WHERE email = @email";
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("email", email);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return (null, null);

            var user = new User
            {
                Id = reader.GetInt32(0),
                Email = reader.GetString(1),
                AuthHash = (byte[])reader["auth_hash"],
                KdfSalt = (byte[])reader["kdf_salt"],
                KdfIterations = reader.GetInt32(4)
            };

            byte[] derivedKey = CryptoService.DeriveMasterKey(masterPassword, user.KdfSalt, user.KdfIterations);
            byte[] computedHash = CryptoService.ComputeAuthHash(derivedKey);

            // Constant-time comparison to prevent timing attacks
            if (!CryptographicOperations.FixedTimeEquals(user.AuthHash, computedHash))
                return (null, null);

            return (user, derivedKey);
        }

        // 3. Save a new encrypted password
        public async Task AddVaultItemAsync(int userId, string siteName, string? siteUrl, string username, string plainPassword, byte[] masterKey)
        {
            var (cipher, nonce, tag) = CryptoService.Encrypt(plainPassword, masterKey);

            await using var conn = new NpgsqlConnection(_connString);
            await conn.OpenAsync();

            const string sql = @"INSERT INTO vault (user_id, site_name, site_url, account_username, encrypted_password, nonce, auth_tag)
                                 VALUES (@uid, @site, @url, @user, @cipher, @nonce, @tag)";

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("uid", userId);
            cmd.Parameters.AddWithValue("site", siteName);
            cmd.Parameters.AddWithValue("url", (object?)siteUrl ?? System.DBNull.Value);
            cmd.Parameters.AddWithValue("user", username);
            cmd.Parameters.AddWithValue("cipher", cipher);
            cmd.Parameters.AddWithValue("nonce", nonce);
            cmd.Parameters.AddWithValue("tag", tag);

            await cmd.ExecuteNonQueryAsync();
        }

        // 4. Obtener todas las credenciales de un usuario (siguen cifradas en memoria)
        public async Task<List<VaultItem>> GetVaultItemsAsync(int userId)
        {
            var items = new List<VaultItem>();

            await using var conn = new NpgsqlConnection(_connString);
            await conn.OpenAsync();

            const string sql = @"SELECT id, user_id, category_id, site_name, site_url, account_username, 
                                        encrypted_password, nonce, auth_tag, is_favorite 
                                 FROM vault WHERE user_id = @uid ORDER BY site_name ASC";

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("uid", userId);

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(new VaultItem
                {
                    Id = reader.GetInt32(0),
                    UserId = reader.GetInt32(1),
                    CategoryId = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                    SiteName = reader.GetString(3),
                    SiteUrl = reader.IsDBNull(4) ? null : reader.GetString(4),
                    AccountUsername = reader.GetString(5),
                    EncryptedPassword = (byte[])reader["encrypted_password"],
                    Nonce = (byte[])reader["nonce"],
                    AuthTag = (byte[])reader["auth_tag"],
                    IsFavorite = reader.GetBoolean(9)
                });
            }

            return items;
        }

        // 5. Eliminar un registro de la bóveda
        public async Task DeleteVaultItemAsync(int itemId, int userId)
        {

            MessageBoxResult result=MessageBox.Show("Are you sure you want to delete this item? This action cannot be undone.", "Confirm Deletion", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                await using var conn = new NpgsqlConnection(_connString);
                await conn.OpenAsync();

                const string sql = "DELETE FROM vault WHERE id = @id AND user_id = @uid";
                await using var cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("id", itemId);
                cmd.Parameters.AddWithValue("uid", userId);

                await cmd.ExecuteNonQueryAsync();
            }
                
        }

        public async Task UpdateVaultItemAsync(
    int itemId,
    int userId,
    string siteName,
    string username,
    string plainPassword,
    byte[] masterKey)
        {
            var (cipher, nonce, tag) =
                CryptoService.Encrypt(plainPassword, masterKey);

            await using var conn = new NpgsqlConnection(_connString);
            await conn.OpenAsync();

            const string sql = @"
        UPDATE vault
        SET
            site_name = @site,
            account_username = @user,
            encrypted_password = @cipher,
            nonce = @nonce,
            auth_tag = @tag
        WHERE
            id = @id
            AND user_id = @uid";

            await using var cmd = new NpgsqlCommand(sql, conn);

            cmd.Parameters.AddWithValue("id", itemId);
            cmd.Parameters.AddWithValue("uid", userId);
            cmd.Parameters.AddWithValue("site", siteName);
            cmd.Parameters.AddWithValue("user", username);
            cmd.Parameters.AddWithValue("cipher", cipher);
            cmd.Parameters.AddWithValue("nonce", nonce);
            cmd.Parameters.AddWithValue("tag", tag);

            await cmd.ExecuteNonQueryAsync();
        }

        // 6. Actualizar un registro existente en la bóveda
        public async Task UpdateVaultItemAsync(int itemId, int userId, string siteName, string? siteUrl, string username, string plainPassword, byte[] masterKey)
        {
            var (encryptedPassword, nonce, authTag) = CryptoService.Encrypt(plainPassword, masterKey);

            await using var conn = new NpgsqlConnection(_connString);
            await conn.OpenAsync();

            const string sql = @"
        UPDATE vault 
        SET site_name = @siteName,
            site_url = @siteUrl,
            account_username = @username,
            encrypted_password = @pass,
            nonce = @nonce,
            auth_tag = @tag
        WHERE id = @id AND user_id = @uid";

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("siteName", siteName);
            cmd.Parameters.AddWithValue("siteUrl", (object?)siteUrl ?? DBNull.Value);
            cmd.Parameters.AddWithValue("username", username);
            cmd.Parameters.AddWithValue("pass", encryptedPassword);
            cmd.Parameters.AddWithValue("nonce", nonce);
            cmd.Parameters.AddWithValue("tag", authTag);
            cmd.Parameters.AddWithValue("id", itemId);
            cmd.Parameters.AddWithValue("uid", userId);

            await cmd.ExecuteNonQueryAsync();
        }
    }
}
