CREATE TABLE IF NOT EXISTS app_setting_collections (
    collection TEXT NOT NULL,
    id TEXT NOT NULL,
    value JSONB NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_by TEXT,
    PRIMARY KEY (collection, id)
);
