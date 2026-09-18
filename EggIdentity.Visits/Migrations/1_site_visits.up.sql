CREATE TABLE IF NOT EXISTS site_visits_daily (
    site TEXT NOT NULL,
    day DATE NOT NULL,
    visits BIGINT NOT NULL DEFAULT 0,
    visitors BIGINT NOT NULL DEFAULT 0,
    pageviews BIGINT NOT NULL DEFAULT 0,
    duration_seconds BIGINT NOT NULL DEFAULT 0,
    capped BOOLEAN NOT NULL DEFAULT false,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (site, day)
);

CREATE TABLE IF NOT EXISTS site_visit_paths_daily (
    site TEXT NOT NULL,
    day DATE NOT NULL,
    path TEXT NOT NULL,
    pageviews BIGINT NOT NULL DEFAULT 0,
    PRIMARY KEY (site, day, path)
);
