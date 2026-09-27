namespace EggIdentity.Fleet.Tests;

public class ComposeServicesTests {
    [Fact]
    public void List_ReadsEveryServiceInFileOrder() {
        const string compose = """
            services:
              db:
                image: postgres:17
                container_name: egg-postgres
              pgbackup:
                image: prodrigestivill/postgres-backup-local:17
              sidecar: {}
            """;

        var services = ComposeServices.List(compose);

        Assert.Equal(["db", "pgbackup", "sidecar"], services.Select(s => s.Service));
        Assert.Equal("egg-postgres", services[0].ContainerName);
        Assert.Equal("postgres:17", services[0].Image);
        Assert.Null(services[1].ContainerName);
        Assert.Null(services[2].Image);
    }

    [Theory]
    [InlineData("")]
    [InlineData("version: '3'\n")]
    [InlineData("services: []\n")]
    public void List_NoServiceMap_IsEmpty(string compose) => Assert.Empty(ComposeServices.List(compose));
}
