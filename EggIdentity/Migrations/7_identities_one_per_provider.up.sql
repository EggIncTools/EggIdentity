CREATE UNIQUE INDEX IF NOT EXISTS ix_identities_user_provider ON identities(user_id, provider);
