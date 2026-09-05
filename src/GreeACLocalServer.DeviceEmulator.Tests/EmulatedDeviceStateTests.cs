using GreeACLocalServer.DeviceEmulator.Models;

namespace GreeACLocalServer.DeviceEmulator.Tests;

public class EmulatedDeviceStateTests
{
    private static EmulatedDeviceState CreateState() => new()
    {
        Mac = "aabbccddeeff",
        CryptoKey = "0123456789abcdef",
    };

    [Fact]
    public void ValueForColumn_TextColumns_ReturnStrings()
    {
        var state = CreateState();
        state.Name = "Living Room";
        state.Host = "server.example.com";
        state.Hid = "100000000000+U-TESTV1.00.bin";

        Assert.Equal("Living Room", Assert.IsType<string>(state.ValueForColumn("name")));
        Assert.Equal("server.example.com", Assert.IsType<string>(state.ValueForColumn("host")));
        Assert.Equal("100000000000+U-TESTV1.00.bin", Assert.IsType<string>(state.ValueForColumn("hid")));
    }

    [Fact]
    public void ValueForColumn_OperatingStateColumns_ReturnIntegers()
    {
        var state = CreateState();
        state.Pow = true;
        state.Mode = 4;
        state.SetTem = 22;
        state.TemUn = 1;
        state.TemSen = 65;

        Assert.Equal(1, Assert.IsType<int>(state.ValueForColumn("Pow")));
        Assert.Equal(4, Assert.IsType<int>(state.ValueForColumn("Mod")));
        Assert.Equal(22, Assert.IsType<int>(state.ValueForColumn("SetTem")));
        Assert.Equal(1, Assert.IsType<int>(state.ValueForColumn("TemUn")));
        Assert.Equal(65, Assert.IsType<int>(state.ValueForColumn("TemSen")));
    }

    [Fact]
    public void ValueForColumn_PowOff_ReturnsZero()
    {
        var state = CreateState();
        state.Pow = false;

        Assert.Equal(0, Assert.IsType<int>(state.ValueForColumn("Pow")));
    }

    [Fact]
    public void ValueForColumn_TemSenUnsupported_ReturnsZeroRegardlessOfStoredValue()
    {
        var state = CreateState();
        state.TemSen = 65;
        state.TemSenSupported = false;

        Assert.Equal(0, Assert.IsType<int>(state.ValueForColumn("TemSen")));
    }

    [Fact]
    public void ValueForColumn_TemSenSupported_ReturnsStoredValue()
    {
        var state = CreateState();
        state.TemSen = 65;

        Assert.Equal(65, Assert.IsType<int>(state.ValueForColumn("TemSen")));
    }
}
