using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using GreeACLocalServer.Device.Extensions;
using GreeACLocalServer.Device.Models;

namespace GreeACLocalServer.Api.Tests;

public class GreeServicesOptionsValidationTests
{
    private static IServiceProvider BuildProvider(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        return new ServiceCollection()
            .AddGreeServices(configuration)
            .BuildServiceProvider();
    }

    [Fact]
    public void MissingDomainName_FailsValidation()
    {
        var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["GreeServer:ServerOptions:ExternalIp"] = "127.0.0.1",
            ["GreeServer:EncryptionOptions:DefaultCryptoKey"] = "a3K8Bx%2r8Y7#xDh",
        });

        var ex = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<ServerOptions>>().Value);
        Assert.Contains("DomainName", ex.Message);
    }

    [Fact]
    public void MissingExternalIp_FailsValidation()
    {
        var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["GreeServer:ServerOptions:DomainName"] = "gree.example.com",
            ["GreeServer:EncryptionOptions:DefaultCryptoKey"] = "a3K8Bx%2r8Y7#xDh",
        });

        var ex = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<ServerOptions>>().Value);
        Assert.Contains("ExternalIp", ex.Message);
    }

    [Fact]
    public void MissingDefaultCryptoKey_FailsValidation()
    {
        var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["GreeServer:ServerOptions:DomainName"] = "gree.example.com",
            ["GreeServer:ServerOptions:ExternalIp"] = "127.0.0.1",
        });

        var ex = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<EncryptionOptions>>().Value);
        Assert.Contains("DefaultCryptoKey", ex.Message);
    }

    [Fact]
    public void CompleteConfiguration_PassesValidation()
    {
        var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["GreeServer:ServerOptions:DomainName"] = "gree.example.com",
            ["GreeServer:ServerOptions:ExternalIp"] = "127.0.0.1",
            ["GreeServer:EncryptionOptions:DefaultCryptoKey"] = "a3K8Bx%2r8Y7#xDh",
        });

        var options = provider.GetRequiredService<IOptions<ServerOptions>>().Value;
        Assert.Equal("gree.example.com", options.DomainName);
    }
}
