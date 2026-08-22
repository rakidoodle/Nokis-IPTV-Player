CREATE TABLE IF NOT EXISTS app_metadata (
    key TEXT NOT NULL PRIMARY KEY,
    value TEXT NOT NULL
);

INSERT OR IGNORE INTO app_metadata (key, value)
VALUES ('database_created_utc', strftime('%Y-%m-%dT%H:%M:%fZ', 'now'));
