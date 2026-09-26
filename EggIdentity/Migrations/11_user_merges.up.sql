-- Records every account merge so consuming apps can remap their own user_id-keyed rows.
-- merged_user_id no longer exists in users; kept_user_id is the surviving account.
-- Chains are flattened on write: if the kept account is itself merged later, existing rows are repointed.
CREATE TABLE IF NOT EXISTS user_merges (
    merged_user_id UUID NOT NULL PRIMARY KEY,
    kept_user_id   UUID NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
    merged_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_user_merges_merged_at ON user_merges(merged_at);
