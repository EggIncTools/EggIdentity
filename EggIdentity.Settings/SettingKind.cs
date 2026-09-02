namespace EggIdentity.Settings;

public enum SettingKind {
    Text,
    Bool,
    Number,
    Duration,
    Secret,
    Url,
    Snowflake,
    Enum,
    StringList,
    CidrList,
    Path,
    Json,
    ReadOnly,
}

public enum ApplyTier {
    Live,
    RestartRequired,
    Bootstrap,
}

public enum Sensitivity {
    Plain,
    Secret,
}

public enum SettingSource {
    Default,
    Environment,
    File,
    Database,
}
