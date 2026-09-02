namespace EggIdentity.Settings.Tests;

public class SettingsValidationTests {
    private static SettingDescriptor Of(SettingKind kind, bool required = false, params string[] enumValues) =>
        new("k", "K", "K", "Core", kind, ApplyTier.RestartRequired, Sensitivity.Plain) {
            Required = required,
            EnumValues = enumValues,
        };

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
    public void Json_RejectsMalformedPayloads() {
        Assert.Null(SettingsValidation.Validate(Of(SettingKind.Json), """{"a":1}"""));
        Assert.NotNull(SettingsValidation.Validate(Of(SettingKind.Json), "{"));
    }
}
