using EggIdentity.Host;
using EggIdentity.Tools;
using Xunit;

namespace EggIdentity.Tools.Tests;

public class AuthentikAppImportTests {
    private static string WriteTemp(string content) {
        var path = Path.Combine(Path.GetTempPath(), "eggidentity-import-" + Guid.NewGuid() + ".app");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void ToRow_MapsDotenvKeysToCollectionFields() {
        var values = AppAuthConfigLoader.ParseFile(WriteTemp("""
            Origin=https://egg-ledger.example.com
            ClientId=egl-client
            ClientSecret=egl-secret
            CallbackUrl=https://identity.example.com/auth/callback
            EndSessionUrl=https://auth.example.com/application/o/egl/end-session/
            """));

        var (id, row) = AuthentikAppImport.ToRow(values);

        Assert.Equal("https://egg-ledger.example.com", id);
        Assert.Equal("https://egg-ledger.example.com", row["origin"]);
        Assert.Equal("egl-client", row["client_id"]);
        Assert.Equal("egl-secret", row["client_secret"]);
        Assert.Equal("https://identity.example.com/auth/callback", row["callback_url"]);
        Assert.Equal("https://auth.example.com/application/o/egl/end-session/", row["end_session_url"]);
        Assert.Equal(AuthentikApps.Descriptor.Fields.Select(f => f.Name).Order(), row.Keys.Order());
    }

    [Fact]
    public void ToRow_OptionalEndSessionUrl_IsNull() {
        var (_, row) = AuthentikAppImport.ToRow(new Dictionary<string, string> {
            ["Origin"] = "https://egg-ledger.example.com",
            ["ClientId"] = "c",
            ["ClientSecret"] = "s",
            ["CallbackUrl"] = "https://identity.example.com/auth/callback",
        });

        Assert.Null(row["end_session_url"]);
    }

    [Fact]
    public void ToRow_MissingRequiredKey_Throws() {
        var e = Assert.Throws<InvalidOperationException>(() => AuthentikAppImport.ToRow(new Dictionary<string, string> {
            ["Origin"] = "https://egg-ledger.example.com",
            ["ClientId"] = "c",
        }));

        Assert.Contains("Client secret", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ToRow_UnknownKey_Throws() {
        var e = Assert.Throws<InvalidOperationException>(() => AuthentikAppImport.ToRow(new Dictionary<string, string> {
            ["Origin"] = "https://egg-ledger.example.com",
            ["ClientId"] = "c",
            ["ClientSecret"] = "s",
            ["CallbackUrl"] = "https://identity.example.com/auth/callback",
            ["Bogus"] = "x",
        }));

        Assert.Contains("Bogus", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ToRow_NonUrlOrigin_Throws() {
        Assert.Throws<InvalidOperationException>(() => AuthentikAppImport.ToRow(new Dictionary<string, string> {
            ["Origin"] = "not a url",
            ["ClientId"] = "c",
            ["ClientSecret"] = "s",
            ["CallbackUrl"] = "https://identity.example.com/auth/callback",
        }));
    }
}
