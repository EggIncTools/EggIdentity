using EggIdentity.Styles.Theming;

namespace EggIdentity.Styles.Tests;

public class ThemeModelTests {
    private const string ValidJson =
        """{"$schema":"eggidentity-theme/1","name":"Test","slug":"test","schemaVersion":1,"tokens":{"accent":{"hex":"#ef7559"}},"chroma":{"surfaceTint":0,"gradientHueShift":0,"glowRadius":0,"glowAlpha":0,"hueRotate":{"enabled":false,"seconds":30}},"css":""}""";

    [Fact]
    public void Parse_ValidDocument_ReturnsModelWithNoErrors() {
        var (model, errors) = ThemeModel.Parse(ValidJson, new ThemeTokenRegistry());

        Assert.Empty(errors);
        Assert.NotNull(model);
    }

    [Fact]
    public void Parse_LegacySchema_AlsoParsesCleanly() {
        string json = ValidJson.Replace("eggidentity-theme/1", "egi-theme/1");

        var (model, errors) = ThemeModel.Parse(json, new ThemeTokenRegistry());

        Assert.Empty(errors);
        Assert.NotNull(model);
    }

    [Fact]
    public void Parse_InvalidSlug_ReturnsErrorsAndNullModel() {
        string json = ValidJson.Replace("\"slug\":\"test\"", "\"slug\":\"Test Slug!\"");

        var (model, errors) = ThemeModel.Parse(json, new ThemeTokenRegistry());

        Assert.Null(model);
        Assert.Contains(errors, e => e.Contains("slug"));
    }

    [Fact]
    public void Parse_UnknownToken_ReturnsError_ThenPassesOnceRegistered() {
        string json = ValidJson.Replace("\"accent\":{\"hex\":\"#ef7559\"}", "\"nonexistent-token\":{\"hex\":\"#ef7559\"}");

        var (unregisteredModel, unregisteredErrors) = ThemeModel.Parse(json, new ThemeTokenRegistry());
        Assert.Null(unregisteredModel);
        Assert.Contains(unregisteredErrors, e => e.Contains("unknown token"));

        var registry = new ThemeTokenRegistry().Register("nonexistent-token");
        var (registeredModel, registeredErrors) = ThemeModel.Parse(json, registry);
        Assert.Empty(registeredErrors);
        Assert.NotNull(registeredModel);
    }

    [Fact]
    public void Parse_TokenWithBothHexAndOklch_ReturnsError() {
        string json = ValidJson.Replace("\"accent\":{\"hex\":\"#ef7559\"}", "\"accent\":{\"hex\":\"#fff\",\"l\":0.5}");

        var (model, errors) = ThemeModel.Parse(json, new ThemeTokenRegistry());

        Assert.Null(model);
        Assert.Contains(errors, e => e.Contains("accent"));
    }

    [Fact]
    public void Parse_MalformedJson_ReturnsErrorsWithoutThrowing() {
        var (model, errors) = ThemeModel.Parse("not json", new ThemeTokenRegistry());

        Assert.Null(model);
        Assert.NotEmpty(errors);
    }
}
