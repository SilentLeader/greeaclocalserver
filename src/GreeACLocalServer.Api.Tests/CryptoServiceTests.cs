using System;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Moq;
using GreeACLocalServer.Api.Options;
using GreeACLocalServer.Api.Services;

namespace GreeACLocalServer.Api.Tests;

public class CryptoServiceTests
{
    private readonly CryptoService _cryptoService;
    private readonly string _testCryptoKey = "TestKey123456789"; // 16 char key for AES

    public CryptoServiceTests()
    {
        var serverOptions = new ServerOptions
        {
            CryptoKey = _testCryptoKey
        };
        var options = Mock.Of<IOptions<ServerOptions>>(o => o.Value == serverOptions);
        _cryptoService = new CryptoService(options);
    }

    [Fact]
    public void Constructor_WithNullCryptoKey_ThrowsInvalidOperationException()
    {
        // Arrange
        var serverOptions = new ServerOptions { CryptoKey = null };
        var options = Mock.Of<IOptions<ServerOptions>>(o => o.Value == serverOptions);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => new CryptoService(options));
    }

    [Fact]
    public void Constructor_WithEmptyCryptoKey_WorksButMayFail()
    {
        // Arrange
        var serverOptions = new ServerOptions { CryptoKey = "" };
        var options = Mock.Of<IOptions<ServerOptions>>(o => o.Value == serverOptions);

        // Act - Constructor doesn't validate empty key, but encryption/decryption might fail
        var service = new CryptoService(options);

        // Assert - Service is created but operations may fail
        Assert.NotNull(service);
        
        // Encryption with empty key might work with fallback to default key
        var result = service.Encrypt("test");
        Assert.NotNull(result);
    }

    [Theory]
    [InlineData("Hello World")]
    [InlineData("")]
    [InlineData("Complex JSON: {\"key\":\"value\",\"number\":123,\"array\":[1,2,3]}")]
    [InlineData("Special chars: !@#$%^&*()_+-=[]{}|;':\",./<>?")]
    public void EncryptDecrypt_WithDefaultKey_ReturnsOriginalText(string plaintext)
    {
        // Act
        var encrypted = _cryptoService.Encrypt(plaintext);
        var decrypted = _cryptoService.Decrypt(encrypted);

        // Assert
        Assert.Equal(plaintext, decrypted);
        Assert.NotEqual(plaintext, encrypted); // Should be encrypted
        Assert.True(IsBase64String(encrypted)); // Should be base64 encoded
    }

    [Theory]
    [InlineData("Hello World", "CustomKey1234567")]
    [InlineData("JSON data", "AnotherKey123456")]
    [InlineData("", "TestKeyForEmpty1")]
    public void EncryptDecrypt_WithCustomKey_ReturnsOriginalText(string plaintext, string customKey)
    {
        // Act
        var encrypted = _cryptoService.Encrypt(plaintext, customKey);
        var decrypted = _cryptoService.Decrypt(encrypted, customKey);

        // Assert
        Assert.Equal(plaintext, decrypted);
        Assert.NotEqual(plaintext, encrypted);
        Assert.True(IsBase64String(encrypted));
    }

    [Fact]
    public void EncryptDecrypt_WithDeviceDefaultKey_ReturnsOriginalText()
    {
        // Arrange
        var plaintext = "Device communication test";
        var deviceKey = "a3K8Bx%2r8Y7#xDh"; // Known device key

        // Act
        var encrypted = _cryptoService.Encrypt(plaintext, deviceKey);
        var decrypted = _cryptoService.Decrypt(encrypted, deviceKey);

        // Assert
        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void EncryptDecrypt_WithEmptyKey_UsesDeviceDefaultKey()
    {
        // Arrange
        var plaintext = "Test with empty key";

        // Act
        var encryptedWithEmpty = _cryptoService.Encrypt(plaintext, "");
        var encryptedWithDefault = _cryptoService.Encrypt(plaintext, "a3K8Bx%2r8Y7#xDh");
        
        var decryptedWithEmpty = _cryptoService.Decrypt(encryptedWithEmpty, "");
        var decryptedWithDefault = _cryptoService.Decrypt(encryptedWithDefault, "a3K8Bx%2r8Y7#xDh");

        // Assert
        Assert.Equal(plaintext, decryptedWithEmpty);
        Assert.Equal(plaintext, decryptedWithDefault);
        Assert.Equal(encryptedWithEmpty, encryptedWithDefault); // Should produce same result
    }

    [Fact]
    public void Decrypt_WithWrongKey_ThrowsCryptographicException()
    {
        // Arrange
        var plaintext = "Secret message";
        var encrypted = _cryptoService.Encrypt(plaintext, "CorrectKey123456");

        // Act & Assert
        Assert.Throws<CryptographicException>(() => _cryptoService.Decrypt(encrypted, "WrongKey1234567"));
    }

    [Theory]
    [InlineData("NotBase64!")]
    [InlineData("Invalid==Base64")]
    public void Decrypt_WithInvalidBase64_ThrowsFormatException(string invalidEncrypted)
    {
        // Act & Assert
        Assert.Throws<FormatException>(() => _cryptoService.Decrypt(invalidEncrypted));
    }

    [Fact]
    public void Decrypt_WithEmptyString_ReturnsEmptyOrThrows()
    {
        // Act & Assert - Empty string may be handled differently
        var ex = Record.Exception(() => _cryptoService.Decrypt(""));
        // Empty string might return empty result or throw an exception - both are acceptable
        Assert.True(ex == null || ex is FormatException || ex is ArgumentException);
    }

    [Fact]
    public void Encrypt_DifferentPlaintexts_ProduceDifferentCiphertexts()
    {
        // Arrange
        var plaintext1 = "Message 1";
        var plaintext2 = "Message 2";

        // Act
        var encrypted1 = _cryptoService.Encrypt(plaintext1);
        var encrypted2 = _cryptoService.Encrypt(plaintext2);

        // Assert
        Assert.NotEqual(encrypted1, encrypted2);
    }

    [Fact]
    public void Encrypt_SamePlaintextMultipleTimes_ProducesSameResult()
    {
        // Arrange
        var plaintext = "Consistent message";

        // Act
        var encrypted1 = _cryptoService.Encrypt(plaintext);
        var encrypted2 = _cryptoService.Encrypt(plaintext);

        // Assert - ECB mode should produce same result for same input
        Assert.Equal(encrypted1, encrypted2);
    }

    [Fact]
    public void EncryptDecrypt_LargeText_WorksCorrectly()
    {
        // Arrange
        var largeText = new string('A', 10000); // 10KB of 'A' characters

        // Act
        var encrypted = _cryptoService.Encrypt(largeText);
        var decrypted = _cryptoService.Decrypt(encrypted);

        // Assert
        Assert.Equal(largeText, decrypted);
    }

    [Fact]
    public void EncryptDecrypt_UnicodeText_WorksCorrectly()
    {
        // Arrange
        var unicodeText = "Hello 世界 🌍 Здравствуй мир";

        // Act
        var encrypted = _cryptoService.Encrypt(unicodeText);
        var decrypted = _cryptoService.Decrypt(encrypted);

        // Assert
        Assert.Equal(unicodeText, decrypted);
    }

    private static bool IsBase64String(string s)
    {
        if (string.IsNullOrEmpty(s))
            return false;

        try
        {
            Convert.FromBase64String(s);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
