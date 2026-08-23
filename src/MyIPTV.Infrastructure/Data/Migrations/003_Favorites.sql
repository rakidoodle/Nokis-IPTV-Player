CREATE TABLE IF NOT EXISTS favorites (
    profile_id TEXT NOT NULL,
    content_type INTEGER NOT NULL,
    content_id TEXT NOT NULL,
    title TEXT NOT NULL,
    added_utc TEXT NOT NULL,
    PRIMARY KEY (profile_id, content_type, content_id),
    CONSTRAINT ck_favorites_content_type CHECK (content_type IN (1, 2, 3))
);

CREATE INDEX IF NOT EXISTS ix_favorites_added_utc
ON favorites (added_utc DESC);
