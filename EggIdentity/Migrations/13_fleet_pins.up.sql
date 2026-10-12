CREATE TABLE IF NOT EXISTS fleet_upstream_builds (
    upstream text NOT NULL,
    height bigint NOT NULL,
    sha text NOT NULL,
    published_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (upstream, height)
);

CREATE TABLE IF NOT EXISTS fleet_pin_state (
    app text NOT NULL,
    upstream text NOT NULL,
    pending_version text,
    pending_commit text,
    last_error text,
    updated_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (app, upstream)
);
