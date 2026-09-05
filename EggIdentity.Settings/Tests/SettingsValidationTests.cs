namespace EggIdentity.Settings.Tests;

public class SettingsValidationTests {
    private static SettingDescriptor Of(SettingKind kind, bool required = false, params string[] enumValues) =>
        new("k", "K", "K", "Core", kind, ApplyTier.RestartRequired, Sensitivity.Plain) {
            Required = required,
            EnumValues = enumValues,
        };

    private static readonly CollectionDescriptor Apps = new(
        "deploy.apps", "Apps", "Deploy",
        [
            new FieldDescriptor("name", "Name", SettingKind.Text) { Required = true },
            new FieldDescriptor("image", "Image", SettingKind.Text) { Required = true },
            new FieldDescriptor("repo_url", "Repository", SettingKind.Url),
            new FieldDescriptor("deploy_secret", "Deploy secret", SettingKind.Secret, Sensitivity.Secret),
            new FieldDescriptor("auto_deploy", "Auto deploy", SettingKind.Bool) { Default = "true" },
        ],
        "name");

    private static Dictionary<string, string?> Row(params (string Field, string? Value)[] pairs) =>
        pairs.ToDictionary(p => p.Field, p => p.Value, StringComparer.Ordinal);

    [Fact]
    public void Blank_IsOnlyAnErrorWhenRequired() {
        Assert.Null(SettingsValidation.Validate(Of(SettingKind.Text), ""));
        Assert.NotNull(SettingsValidation.Validate(Of(SettingKind.Text, required: true), ""));
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("nope", false)]
    public void Bool_ChecksParse(string value, bool ok) {
        Assert.Equal(ok, SettingsValidation.Validate(Of(SettingKind.Bool), value) is null);
    }

    [Theory]
    [InlineData("42", true)]
    [InlineData("4.2", false)]
    public void Int_ChecksParse(string value, bool ok) {
        Assert.Equal(ok, SettingsValidation.Validate(Of(SettingKind.Number), value) is null);
    }

    [Theory]
    [InlineData("30s", true)]
    [InlineData("5m", true)]
    [InlineData("1h30m", true)]
    [InlineData("5", false)]
    [InlineData("5x", false)]
    public void Duration_ChecksUnits(string value, bool ok) {
        Assert.Equal(ok, SettingsValidation.Validate(Of(SettingKind.Duration), value) is null);
    }

    [Theory]
    [InlineData("https://example.com", true)]
    [InlineData("not a url", false)]
    public void Url_RequiresAbsolute(string value, bool ok) {
        Assert.Equal(ok, SettingsValidation.Validate(Of(SettingKind.Url), value) is null);
    }

    [Theory]
    [InlineData("192.168.1.0/24", true)]
    [InlineData("2a01:4f8:c012:e15b:8000::/65", true)]
    [InlineData("192.168.1.0", false)]
    [InlineData("192.168.1.0/99", false)]
    public void CidrList_ValidatesEachEntry(string value, bool ok) {
        Assert.Equal(ok, SettingsValidation.Validate(Of(SettingKind.CidrList), value) is null);
    }

    [Fact]
    public void Enum_RestrictsToDeclaredValues() {
        var descriptor = Of(SettingKind.Enum, false, "Local", "Remote");

        Assert.Null(SettingsValidation.Validate(descriptor, "Local"));
        Assert.NotNull(SettingsValidation.Validate(descriptor, "Elsewhere"));
    }

    [Fact]
    public void ReadOnly_IsNeverWritable() {
        Assert.NotNull(SettingsValidation.Validate(Of(SettingKind.ReadOnly), "anything"));
    }

    [Fact]
    public void External_IsNeverWritable() {
        Assert.NotNull(SettingsValidation.Validate(Of(SettingKind.External), "eth0"));
    }

    [Fact]
    public void Json_RejectsMalformedPayloads() {
        Assert.Null(SettingsValidation.Validate(Of(SettingKind.Json), """{"a":1}"""));
        Assert.NotNull(SettingsValidation.Validate(Of(SettingKind.Json), "{"));
    }

    [Fact]
    public void ValidateKind_IsSharedByScalarsAndFields() {
        Assert.Null(SettingsValidation.ValidateKind(SettingKind.Number, "7", []));
        Assert.NotNull(SettingsValidation.ValidateKind(SettingKind.Number, "seven", []));
        Assert.Null(SettingsValidation.ValidateKind(SettingKind.Number, "", []));
    }

    [Fact]
    public void Row_AcceptsAWellFormedRecord() {
        var row = Row(("name", "eggledger"), ("image", "ghcr.io/x/y:latest"), ("repo_url", "https://github.com/x/y"));

        Assert.Null(SettingsValidation.ValidateRow(Apps, row));
    }

    [Fact]
    public void Row_RejectsUnknownFields() {
        var error = SettingsValidation.ValidateRow(Apps, Row(("name", "a"), ("image", "b"), ("bogus", "c")));

        Assert.Contains("bogus", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("has space")]
    [InlineData("semi;colon")]
    public void Row_RejectsBadIds(string? id) {
        Assert.NotNull(SettingsValidation.ValidateRow(Apps, Row(("name", id), ("image", "b"))));
    }

    [Theory]
    [InlineData("eggledger")]
    [InlineData("https://ledger.example.com/")]
    [InlineData("user@host:1.2-3_4")]
    public void Row_AcceptsPathSafeIds(string id) {
        Assert.Null(SettingsValidation.ValidateRow(Apps, Row(("name", id), ("image", "b"))));
    }

    [Fact]
    public void Row_RequiresRequiredFields() {
        var error = SettingsValidation.ValidateRow(Apps, Row(("name", "a")));

        Assert.Equal("Image is required", error);
    }

    [Fact]
    public void Row_AppliesKindRulesPerField() {
        var error = SettingsValidation.ValidateRow(Apps, Row(("name", "a"), ("image", "b"), ("repo_url", "not a url")));

        Assert.StartsWith("Repository:", error, StringComparison.Ordinal);
        Assert.NotNull(SettingsValidation.ValidateRow(Apps, Row(("name", "a"), ("image", "b"), ("auto_deploy", "maybe"))));
    }

    [Fact]
    public void Row_OptionalBlankFieldsAreFine() {
        Assert.Null(SettingsValidation.ValidateRow(Apps, Row(("name", "a"), ("image", "b"), ("deploy_secret", ""), ("auto_deploy", null))));
    }
}
