CREATE TABLE IF NOT EXISTS epg_cache (
    source_key TEXT NOT NULL PRIMARY KEY,
    fetched_utc TEXT NOT NULL,
    expires_utc TEXT NOT NULL,
    entity_tag TEXT NULL,
    last_modified_utc TEXT NULL,
    program_count INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS epg_channels (
    source_key TEXT NOT NULL,
    channel_id TEXT NOT NULL,
    display_name TEXT NOT NULL,
    PRIMARY KEY (source_key, channel_id)
);

CREATE TABLE IF NOT EXISTS epg_programs (
    source_key TEXT NOT NULL,
    channel_id TEXT NOT NULL,
    start_utc TEXT NOT NULL,
    end_utc TEXT NOT NULL,
    title TEXT NOT NULL,
    description TEXT NULL,
    PRIMARY KEY (source_key, channel_id, start_utc, title)
);

CREATE INDEX IF NOT EXISTS ix_epg_programs_lookup
ON epg_programs (source_key, channel_id, start_utc, end_utc);
