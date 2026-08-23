CREATE TABLE IF NOT EXISTS channels (
    profile_id TEXT NOT NULL,
    channel_id TEXT NOT NULL,
    name TEXT NOT NULL,
    group_name TEXT NULL,
    epg_id TEXT NULL,
    PRIMARY KEY (profile_id, channel_id),
    FOREIGN KEY (profile_id) REFERENCES profiles(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_channels_profile_group_name ON channels(profile_id, group_name, name);
CREATE INDEX IF NOT EXISTS ix_channels_name ON channels(name COLLATE NOCASE);

CREATE TABLE IF NOT EXISTS movies (
    profile_id TEXT NOT NULL,
    movie_id TEXT NOT NULL,
    name TEXT NOT NULL,
    category_id TEXT NOT NULL,
    rating TEXT NULL,
    container_extension TEXT NULL,
    description TEXT NULL,
    year TEXT NULL,
    duration TEXT NULL,
    PRIMARY KEY (profile_id, movie_id),
    FOREIGN KEY (profile_id) REFERENCES profiles(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_movies_profile_category_name ON movies(profile_id, category_id, name);
CREATE INDEX IF NOT EXISTS ix_movies_name ON movies(name COLLATE NOCASE);

CREATE TABLE IF NOT EXISTS series (
    profile_id TEXT NOT NULL,
    series_id TEXT NOT NULL,
    name TEXT NOT NULL,
    category_id TEXT NOT NULL,
    plot TEXT NULL,
    genre TEXT NULL,
    rating TEXT NULL,
    release_date TEXT NULL,
    PRIMARY KEY (profile_id, series_id),
    FOREIGN KEY (profile_id) REFERENCES profiles(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_series_profile_category_name ON series(profile_id, category_id, name);
CREATE INDEX IF NOT EXISTS ix_series_name ON series(name COLLATE NOCASE);

CREATE TABLE IF NOT EXISTS episodes (
    profile_id TEXT NOT NULL,
    episode_id TEXT NOT NULL,
    series_id TEXT NOT NULL,
    season_number INTEGER NOT NULL,
    episode_number INTEGER NOT NULL,
    name TEXT NOT NULL,
    container_extension TEXT NULL,
    plot TEXT NULL,
    duration TEXT NULL,
    PRIMARY KEY (profile_id, episode_id),
    FOREIGN KEY (profile_id, series_id) REFERENCES series(profile_id, series_id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_episodes_series_order ON episodes(profile_id, series_id, season_number, episode_number);
CREATE INDEX IF NOT EXISTS ix_episodes_name ON episodes(name COLLATE NOCASE);

CREATE TABLE IF NOT EXISTS settings (
    setting_key TEXT NOT NULL PRIMARY KEY,
    value_json TEXT NOT NULL,
    updated_utc TEXT NOT NULL
);
