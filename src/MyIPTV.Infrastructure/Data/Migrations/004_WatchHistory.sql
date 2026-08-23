CREATE TABLE IF NOT EXISTS watch_history (
    profile_id TEXT NOT NULL,
    content_type INTEGER NOT NULL,
    content_id TEXT NOT NULL,
    title TEXT NOT NULL,
    last_watched_utc TEXT NOT NULL,
    position_ticks INTEGER NOT NULL DEFAULT 0,
    duration_ticks INTEGER NULL,
    PRIMARY KEY (profile_id, content_type, content_id),
    CONSTRAINT ck_watch_history_content_type CHECK (content_type IN (1, 2, 3)),
    CONSTRAINT ck_watch_history_position CHECK (position_ticks >= 0),
    CONSTRAINT ck_watch_history_duration CHECK (duration_ticks IS NULL OR duration_ticks >= 0)
);

CREATE INDEX IF NOT EXISTS ix_watch_history_last_watched
ON watch_history (last_watched_utc DESC);
