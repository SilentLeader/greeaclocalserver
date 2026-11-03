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

    [Theory]
    [InlineData("\"a3K8Bx%2r8Y7#xDh\"")] // quotes kept by compose list syntax
    [InlineData("a3K8Bx%2r8Y7")]         // value cut at '#'
    [InlineData("short")]
    public void WrongLengthDefaultCryptoKey_FailsValidation(string key)
    {
        var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["GreeServer:ServerOptions:DomainName"] = "gree.example.com",
            ["GreeServer:ServerOptions:ExternalIp"] = "127.0.0.1",
            ["GreeServer:EncryptionOptions:DefaultCryptoKey"] = key,
        });

        var ex = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<EncryptionOptions>>().Value);
        Assert.Contains("16, 24 or 32 bytes", ex.Message);
    }

    [Theory]
    [InlineData("a3K8Bx%2r8Y7#xDh")]
    [InlineData("0123456789abcdef01234567")]
    [InlineData("0123456789abcdef0123456789abcdef")]
    public void ValidLengthDefaultCryptoKey_PassesValidation(string key)
    {
        var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["GreeServer:ServerOptions:DomainName"] = "gree.example.com",
            ["GreeServer:ServerOptions:ExternalIp"] = "127.0.0.1",
            ["GreeServer:EncryptionOptions:DefaultCryptoKey"] = key,
        });

        var options = provider.GetRequiredService<IOptions<EncryptionOptions>>().Value;
        Assert.Equal(key, options.DefaultCryptoKey);
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
