using Microsoft.Extensions.Configuration;
using GreeACLocalServer.Api.Infrastructure;

namespace GreeACLocalServer.Api.Tests;

public class ConfigurationModulesTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _etcDir;

    public ConfigurationModulesTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "greeac-config-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _etcDir = Path.Combine(_tempDir, "etc");
        Directory.CreateDirectory(_etcDir);
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

        GC.SuppressFinalize(this);
    }

    private void WriteFile(string name, string contents) =>
        File.WriteAllText(Path.Combine(_tempDir, name), contents);

    private void WriteEtcFile(string name, string contents) =>
        File.WriteAllText(Path.Combine(_etcDir, name), contents);

    private IConfigurationRoot Build(string? environmentName, string[]? args = null, bool useEtc = false) =>
        new ConfigurationBuilder()
            .SetBasePath(_tempDir)
            .AddGreeConfiguration(environmentName, args, useEtc ? _etcDir : null)
            .Build();

    [Fact]
    public void EnvironmentSpecificFile_OverridesBaseAppsettings()
    {
        WriteFile("appsettings.json", """{ "GreeServer": { "ServerOptions": { "DomainName": "base.example.com", "ExternalIp": "127.0.0.1" } } }""");
        WriteFile("appsettings.Testenv.json", """{ "GreeServer": { "ServerOptions": { "DomainName": "testenv.example.com" } } }""");

        var config = Build("Testenv");

        Assert.Equal("testenv.example.com", config["GreeServer:ServerOptions:DomainName"]);
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
    public void EnvironmentVariables_OverrideJsonFiles()
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

    [Fact]
    public void CommandLineArgs_OverrideEnvironmentVariables()
    {
        WriteFile("appsettings.json", """{ "GreeServer": { "ServerOptions": { "DomainName": "base.example.com" } } }""");

        const string key = "GreeServer__ServerOptions__DomainName";
        Environment.SetEnvironmentVariable(key, "env.example.com");
        try
        {
            var config = Build("Testenv", ["--GreeServer:ServerOptions:DomainName=cli.example.com"]);
            Assert.Equal("cli.example.com", config["GreeServer:ServerOptions:DomainName"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    [Fact]
    public void CommandLineArgs_OverrideJsonFiles()
    {
        WriteFile("appsettings.json", """{ "GreeServer": { "ServerOptions": { "DomainName": "base.example.com" } } }""");

        var config = Build(null, ["--GreeServer:ServerOptions:DomainName=cli.example.com"]);

        Assert.Equal("cli.example.com", config["GreeServer:ServerOptions:DomainName"]);
    }

    [Fact]
    public void EtcOverride_BeatsBaseAppsettings()
    {
        WriteFile("appsettings.json", """{ "GreeServer": { "ServerOptions": { "DomainName": "base.example.com" } } }""");
        WriteEtcFile("appsettings.json", """{ "GreeServer": { "ServerOptions": { "DomainName": "etc.example.com" } } }""");

        var config = Build(null, useEtc: true);

        Assert.Equal("etc.example.com", config["GreeServer:ServerOptions:DomainName"]);
    }

    [Fact]
    public void EtcOverride_BeatsEnvironmentSpecificAppsettings()
    {
        WriteFile("appsettings.json", """{ "GreeServer": { "ServerOptions": { "DomainName": "base.example.com" } } }""");
        WriteFile("appsettings.Testenv.json", """{ "GreeServer": { "ServerOptions": { "DomainName": "testenv.example.com" } } }""");
        WriteEtcFile("appsettings.json", """{ "GreeServer": { "ServerOptions": { "DomainName": "etc.example.com" } } }""");

        var config = Build("Testenv", useEtc: true);

        Assert.Equal("etc.example.com", config["GreeServer:ServerOptions:DomainName"]);
    }

    [Fact]
    public void EnvironmentVariables_OverrideEtcOverrides()
    {
        WriteFile("appsettings.json", """{ "GreeServer": { "ServerOptions": { "DomainName": "base.example.com" } } }""");
        WriteEtcFile("appsettings.json", """{ "GreeServer": { "ServerOptions": { "DomainName": "etc.example.com" } } }""");

        const string key = "GreeServer__ServerOptions__DomainName";
        Environment.SetEnvironmentVariable(key, "env.example.com");
        try
        {
            var config = Build(null, useEtc: true);
            Assert.Equal("env.example.com", config["GreeServer:ServerOptions:DomainName"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    [Fact]
    public void AppsettingsDev_BeatsEtcOverride()
    {
        WriteFile("appsettings.json", """{ "GreeServer": { "ServerOptions": { "DomainName": "base.example.com" } } }""");
        WriteEtcFile("appsettings.json", """{ "GreeServer": { "ServerOptions": { "DomainName": "etc.example.com" } } }""");
        WriteFile("appsettings.dev.json", """{ "GreeServer": { "ServerOptions": { "DomainName": "dev.example.com" } } }""");

        var config = Build(null, useEtc: true);

        Assert.Equal("dev.example.com", config["GreeServer:ServerOptions:DomainName"]);
    }

    [Fact]
    public void NullOrEmptyArgs_DoNotThrow()
    {
        WriteFile("appsettings.json", """{ "GreeServer": { "ServerOptions": { "DomainName": "base.example.com" } } }""");

        var fromNull = Build(null, null);
        var fromEmpty = Build(null, []);

        Assert.Equal("base.example.com", fromNull["GreeServer:ServerOptions:DomainName"]);
        Assert.Equal("base.example.com", fromEmpty["GreeServer:ServerOptions:DomainName"]);
    }
}
