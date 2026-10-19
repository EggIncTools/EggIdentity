using System.Text.Json.Serialization;

namespace EggIdentity.Styles.Theming;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(ThemeModel))]
[JsonSerializable(typeof(ThemeTokenValue))]
[JsonSerializable(typeof(ThemeChroma))]
[JsonSerializable(typeof(ThemeHueRotate))]
internal sealed partial class ThemeJsonContext : JsonSerializerContext {
}
