-- 1. Crear la base de datos dedicada
CREATE DATABASE password_vault
    WITH ENCODING = 'UTF8'
    LC_COLLATE = 'en_US.UTF-8'
    LC_CTYPE = 'en_US.UTF-8';

-- 2. Crear un rol específico para tu aplicación de escritorio
-- Cambia 'TuPasswordSeguraAqui123!' por una clave fuerte
CREATE USER vault_app_user WITH PASSWORD 'TuPasswordSeguraAqui123!';

-- 3. Dar acceso a la base de datos
GRANT CONNECT ON DATABASE password_vault TO vault_app_user;
