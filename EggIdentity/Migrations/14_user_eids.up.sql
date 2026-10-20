CREATE TABLE IF NOT EXISTS user_eids (
    user_id UUID NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
    eid TEXT NOT NULL,
    position INT NOT NULL,
    label TEXT,
    snapshot JSONB,
    snapshot_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (user_id, eid),
    CONSTRAINT user_eids_position_unique UNIQUE (user_id, position) DEFERRABLE INITIALLY DEFERRED
);
