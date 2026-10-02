using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace EggIdentity.Testing;

public sealed class FakeWebHostEnvironment : IWebHostEnvironment {
    public FakeWebHostEnvironment(string? contentRoot = null, string environmentName = "Testing") {
        ContentRootPath = contentRoot ?? AppContext.BaseDirectory;
        WebRootPath = Path.Combine(ContentRootPath, "wwwroot");
        EnvironmentName = environmentName;
        ContentRootFileProvider = Provider(ContentRootPath);
        WebRootFileProvider = Provider(WebRootPath);
    }

    public string ApplicationName { get; set; } = "EggIdentity.Testing";

    public string EnvironmentName { get; set; }

    public string ContentRootPath { get; set; }

    public IFileProvider ContentRootFileProvider { get; set; }

    public string WebRootPath { get; set; }

    public IFileProvider WebRootFileProvider { get; set; }

    public bool IsProduction => EnvironmentName == Environments.Production;

    private static IFileProvider Provider(string path) => Directory.Exists(path) ? new PhysicalFileProvider(path) : new NullFileProvider();
}
