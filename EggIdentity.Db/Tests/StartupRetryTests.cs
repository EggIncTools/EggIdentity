using System.Net.Sockets;
using EggIdentity.Resilience;
using Npgsql;

namespace EggIdentity.Db.Tests;

public class StartupRetryTests {
    [Fact]
    public void ADnsFailureIsTransient_BecauseThatIsTheCrashloopWeAreFixing() {
        var dns = new NpgsqlException("Resource temporarily unavailable", new SocketException(11));

        Assert.True(Database.IsTransient(dns));
    }

    [Fact]
    public void ARefusedConnectionIsTransient() =>
        Assert.True(Database.IsTransient(new NpgsqlException("refused", new SocketException(111))));

    [Fact]
    public void ATimeoutIsTransient() =>
        Assert.True(Database.IsTransient(new TimeoutException("timed out")));

    [Fact]
    public void PostgresStillStartingIsTransient() =>
        Assert.True(Database.IsTransient(new PostgresException(
            "the database system is starting up", "FATAL", "FATAL", PostgresErrorCodes.CannotConnectNow)));

    [Fact]
    public void AWrongPasswordIsNotTransient_SoItFailsFastAndLoud() =>
        Assert.False(Database.IsTransient(new PostgresException(
            "password authentication failed", "FATAL", "FATAL", PostgresErrorCodes.InvalidPassword)));

    [Fact]
    public void AMissingDatabaseIsNotTransient() =>
        Assert.False(Database.IsTransient(new PostgresException(
            "database does not exist", "FATAL", "FATAL", PostgresErrorCodes.InvalidCatalogName)));

    [Fact]
    public void AUriConnectionStringIsNotTransient_BecauseRetryingCannotFixIt() =>
        Assert.False(Database.IsTransient(new ArgumentException("connection string is a URI")));

    [Fact]
    public void TheRetryBudgetIsBoundedRatherThanInfinite() {
        Assert.Equal(8, Database.StartupRetry.MaxAttempts);
        Assert.True(Database.StartupRetry.MaxDelay <= TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task ATransientFailureIsRetriedUntilItSucceeds() {
        var time = new FakeTimeProvider();
        var attempts = 0;

        var result = await Retry.RunAsync(
            _ => {
                attempts++;
                return attempts < 3
                    ? throw new NpgsqlException("dns", new SocketException(11))
                    : Task.FromResult("connected");
            },
            Database.StartupRetry, time, CancellationToken.None);

        Assert.Equal("connected", result);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task APermanentFailureIsNotRetried() {
        var time = new FakeTimeProvider();
        var attempts = 0;

        await Assert.ThrowsAsync<PostgresException>(() => Retry.RunAsync<string>(
            _ => {
                attempts++;
                throw new PostgresException(
                    "password authentication failed", "FATAL", "FATAL", PostgresErrorCodes.InvalidPassword);
            },
            Database.StartupRetry, time, CancellationToken.None));

        Assert.Equal(1, attempts);
    }

    private sealed class FakeTimeProvider : TimeProvider {
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) {
            ArgumentNullException.ThrowIfNull(callback);
            return new Immediate(callback, state);
        }

        private sealed class Immediate : ITimer {
            public Immediate(TimerCallback callback, object? state) => callback(state);

            public bool Change(TimeSpan dueTime, TimeSpan period) => true;

            public void Dispose() {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
