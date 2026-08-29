CREATE TABLE github_sponsor_status (
    user_id uuid PRIMARY KEY REFERENCES users(user_id) ON DELETE CASCADE,
    is_sponsor boolean NOT NULL DEFAULT false,
    last_synced_at timestamptz,
    updated_at timestamptz NOT NULL DEFAULT now()
);
