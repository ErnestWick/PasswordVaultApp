-- ==========================================
-- TABLE 1: USERS (Vault owners)
-- ==========================================
CREATE TABLE users (
    id SERIAL PRIMARY KEY,
    email VARCHAR(255) UNIQUE NOT NULL,
    auth_hash BYTEA NOT NULL,           -- SHA-256/BCrypt hash of masterKey to validate login
    kdf_salt BYTEA NOT NULL,            -- 16-byte random salt for PBKDF2/Argon2
    kdf_iterations INTEGER NOT NULL DEFAULT 600000,
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP
);

-- ==========================================
-- TABLE 2: CATEGORIES (Optional organization)
-- ==========================================
CREATE TABLE categories (
    id SERIAL PRIMARY KEY,
    user_id INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    name VARCHAR(80) NOT NULL,
    UNIQUE(user_id, name)               -- Prevents duplicate category names per user
);

-- ==========================================
-- TABLE 3: VAULT (Encrypted credentials)
-- ==========================================
CREATE TABLE vault (
    id SERIAL PRIMARY KEY,
    user_id INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    category_id INTEGER REFERENCES categories(id) ON DELETE SET NULL,
    site_name VARCHAR(150) NOT NULL,
    site_url TEXT,
    account_username VARCHAR(255) NOT NULL,
    
    -- AES-256-GCM cryptographic fields
    encrypted_password BYTEA NOT NULL,  -- Ciphertext
    nonce BYTEA NOT NULL,               -- Unique 12-byte initialization vector
    auth_tag BYTEA NOT NULL,            -- 16-byte authentication tag
    
    -- Optional encrypted notes
    encrypted_notes BYTEA,
    notes_nonce BYTEA,
    notes_tag BYTEA,
    
    is_favorite BOOLEAN NOT NULL DEFAULT FALSE,
    created_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ DEFAULT CURRENT_TIMESTAMP
);

-- ==========================================
-- INDEXES & TRIGGERS
-- ==========================================
CREATE INDEX idx_vault_user ON vault(user_id);

CREATE OR REPLACE FUNCTION update_timestamp()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_vault_updated
BEFORE UPDATE ON vault
FOR EACH ROW
EXECUTE FUNCTION update_timestamp();

-- ==========================================
-- PERMISSIONS FOR THE APP USER
-- ==========================================
GRANT USAGE ON SCHEMA public TO vault_app_user;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO vault_app_user;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO vault_app_user;
