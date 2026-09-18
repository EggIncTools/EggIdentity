CREATE TABLE IF NOT EXISTS cookie_consent (
    user_id UUID PRIMARY KEY REFERENCES users(user_id) ON DELETE CASCADE,
    functional BOOLEAN NOT NULL,
    analytics BOOLEAN NOT NULL,
    policy_version INTEGER NOT NULL,
    decided_at TIMESTAMPTZ NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
