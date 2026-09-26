namespace EggIdentity.Host.Tests;

public class AuthentikAdminClientTests {
    private const string Users = """
        {"results":[
          {"pk":7,"username":"alice","uid":"hash-alice","uuid":"11111111-1111-1111-1111-111111111111","email":"alice@example.com"},
          {"pk":9,"username":"bob","uid":"hash-bob","uuid":"22222222-2222-2222-2222-222222222222","email":"bob@example.com"}
        ]}
        """;

    [Theory]
    [InlineData("hash-bob", "9")]
    [InlineData("22222222-2222-2222-2222-222222222222", "9")]
    [InlineData("bob@example.com", "9")]
    [InlineData("7", "7")]
    [InlineData("alice", "7")]
    public void ParseUserPk_MatchesSubAgainstEverySubMode(string sub, string expectedPk) =>
        Assert.Equal(expectedPk, AuthentikAdminClient.ParseUserPk(Users, sub));

    [Fact]
    public void ParseUserPk_NoMatch_ReturnsNull() =>
        Assert.Null(AuthentikAdminClient.ParseUserPk(Users, "hash-nobody"));

    [Fact]
    public void ParseUserPk_NoResults_ReturnsNull() =>
        Assert.Null(AuthentikAdminClient.ParseUserPk("""{"results":[]}""", "x"));

    [Fact]
    public void ParseConnections_ReadsPkIdentifierAndSource() {
        const string json = """
            {"results":[
              {"pk":41,"user":7,"source":"aaaa","source_obj":{"pk":"aaaa","name":"Discord","slug":"discord","component":"ak-source-oauth-form"},"identifier":"258383847148879874"},
              {"pk":42,"user":7,"source":"bbbb","source_obj":{"pk":"bbbb","name":"GitHub","slug":"github","component":"ak-source-oauth-form"},"identifier":"40234707"},
              {"pk":43,"user":7,"source":"cccc","source_obj":{"pk":"cccc","name":"Google","slug":"google","component":"ak-source-oauth-form"},"identifier":"1029384756"},
              {"pk":44,"user":7,"source":"dddd","source_obj":{"pk":"dddd","name":"Microsoft","slug":"microsoft-entra","component":"ak-source-oauth-form"},"identifier":"ms-oid-1"},
              {"pk":45,"user":7,"source":"eeee","source_obj":{"pk":"eeee","name":"LDAP","slug":"corp-ldap","component":"ak-source-ldap-form"},"identifier":"cn=alice"},
              {"pk":46,"user":7,"source":"ffff","source_obj":{"pk":"ffff","name":"Discord","slug":"discord"},"identifier":""}
            ]}
            """;

        var connections = AuthentikAdminClient.ParseConnections(json);

        Assert.Equal(5, connections.Count);
        Assert.Equal(["discord", "github", "google", "microsoft", null], connections.Select(c => c.Provider));
        Assert.Equal("258383847148879874", connections[0].Identifier);
        Assert.Equal(44, connections[3].Pk);
    }

    [Theory]
    [InlineData("discord", "Discord", "discord")]
    [InlineData("github-oauth", "GitHub", "github")]
    [InlineData("login-google", "Google Workspace", "google")]
    [InlineData("entra", "Microsoft", "microsoft")]
    [InlineData("corp-ldap", "LDAP", null)]
    public void MapProvider_UsesSlugThenName(string slug, string name, string? expected) =>
        Assert.Equal(expected, AuthentikAdminClient.MapProvider(slug, name));
}
