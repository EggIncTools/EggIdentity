CREATE TABLE IF NOT EXISTS site_visit_salts (
    site TEXT NOT NULL,
    day DATE NOT NULL,
    salt BYTEA NOT NULL,
    PRIMARY KEY (site, day)
);

CREATE TABLE IF NOT EXISTS site_visitors_daily (
    site TEXT NOT NULL,
    day DATE NOT NULL,
    visitor TEXT NOT NULL,
    last_seen BIGINT NOT NULL,
    PRIMARY KEY (site, day, visitor)
);
