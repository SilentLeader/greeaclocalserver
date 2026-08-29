using Microsoft.Extensions.Configuration;
using GreeACLocalServer.Api.Infrastructure;

namespace GreeACLocalServer.Api.Tests;

public class ConfigurationModulesTests : IDisposable
{
    private readonly string _tempDir;

    public ConfigurationModulesTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "greeac-config-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        catch (IOException)
        {
            // best effort
        }
    }

    private void WriteFile(string name, string contents) =>
        File.WriteAllText(Path.Combine(_tempDir, name), contents);

    private IConfigurationRoot Build(string? environmentName) =>
        new ConfigurationBuilder()
            .SetBasePath(_tempDir)
            .BuildConfiguration(environmentName)
            .Build();

    [Fact]
    public void EnvironmentSpecificFile_OverridesBaseAppsettings()
    {
        WriteFile("appsettings.json", """{ "GreeServer": { "ServerOptions": { "DomainName": "base.example.com", "ExternalIp": "127.0.0.1" } } }""");
        WriteFile("appsettings.Testenv.json", """{ "GreeServer": { "ServerOptions": { "DomainName": "testenv.example.com" } } }""");

        var config = Build("Testenv");

        Assert.Equal("testenv.example.com", config["GreeServer:ServerOptions:DomainName"]);
        // Untouched keys still come from the base file.
        Assert.Equal("127.0.0.1", config["GreeServer:ServerOptions:ExternalIp"]);
    }

    [Fact]
    public void EnvironmentSpecificFile_IsOptional()
    {
        WriteFile("appsettings.json", """{ "GreeServer": { "ServerOptions": { "DomainName": "base.example.com" } } }""");

        var config = Build("DoesNotExist");

        Assert.Equal("base.example.com", config["GreeServer:ServerOptions:DomainName"]);
    }

    [Fact]
    public void EnvironmentVariable_OverridesJsonFiles()
    {
        WriteFile("appsettings.json", """{ "GreeServer": { "ServerOptions": { "DomainName": "base.example.com" } } }""");
        WriteFile("appsettings.Testenv.json", """{ "GreeServer": { "ServerOptions": { "DomainName": "testenv.example.com" } } }""");

        const string key = "GreeServer__ServerOptions__DomainName";
        Environment.SetEnvironmentVariable(key, "env.example.com");
        try
        {
            var config = Build("Testenv");
            Assert.Equal("env.example.com", config["GreeServer:ServerOptions:DomainName"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }
}
