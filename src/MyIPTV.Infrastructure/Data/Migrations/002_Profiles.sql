CREATE TABLE IF NOT EXISTS profiles (
    id TEXT NOT NULL PRIMARY KEY,
    name TEXT NOT NULL,
    connection_type INTEGER NOT NULL,
    server_address TEXT NOT NULL,
    username TEXT NULL,
    created_utc TEXT NOT NULL,
    updated_utc TEXT NOT NULL,
    CONSTRAINT ck_profiles_connection_type CHECK (connection_type IN (1, 2, 3))
);

CREATE INDEX IF NOT EXISTS ix_profiles_name
ON profiles (name COLLATE NOCASE);
