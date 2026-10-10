using EggIdentity.Contract;
using EggIdentity.Settings;

namespace EggIdentity.Deploy.Tests;

public class StackImportTests {
    private const string Stack = "egg-postgres";

    private static readonly StackService Postgres = new("db", "egg-postgres", "postgres:17");
    private static readonly StackService Backup = new("pgbackup", "egg-pgbackup", "prodrigestivill/postgres-backup-local:17");
    private static readonly StackService Bare = new("worker", null, "ghcr.io/x/worker:latest");

    [Fact]
    public void Plan_ClassifiesNewRegisteredAndConflict() {
        IReadOnlyList<SuiteApp> rows = [
            new SuiteApp { Name = "postgres", Stack = "EGG-POSTGRES", Container = "egg-postgres" },
            new SuiteApp { Name = "egg-pgbackup", Stack = "other-stack" },
        ];

        var plan = StackImport.Plan(Stack, [Postgres, Backup, Bare], rows);

        Assert.Equal([StackImportState.Registered, StackImportState.Conflict, StackImportState.New], plan.Select(c => c.State));
        Assert.Equal("postgres", plan[0].ExistingApp);
        Assert.Equal("egg-pgbackup", plan[1].ProposedName);
        Assert.Equal("worker", plan[2].ProposedName);
        Assert.Equal("worker", plan[2].ContainerName);
    }

    [Fact]
    public void Values_BindToARowThatDoesNotAutoDeploy() {
        var candidate = StackImport.Plan(Stack, [Postgres], [])[0];

        var app = CollectionBinder.Bind<SuiteApp>(StackImport.Values(Stack, candidate, " egg-postgres ", SuiteApp.SubProdEnvironment));

        Assert.Equal("egg-postgres", app.Name);
        Assert.Equal(Stack, app.Stack);
        Assert.Null(app.Container);
        Assert.Equal("egg-postgres", app.ContainerName);
        Assert.False(app.AutoDeploy);
        Assert.True(app.Enabled);
        Assert.True(app.IsSubProd);
    }

    [Fact]
    public void Values_RenamedRowKeepsTheContainer() {
        var candidate = StackImport.Plan(Stack, [Postgres], [])[0];

        var app = CollectionBinder.Bind<SuiteApp>(StackImport.Values(Stack, candidate, "postgres", SuiteApp.ProdEnvironment));

        Assert.Equal("postgres", app.Name);
        Assert.Equal("egg-postgres", app.ContainerName);
    }
}
