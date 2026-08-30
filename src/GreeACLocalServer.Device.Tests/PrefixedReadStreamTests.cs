using System.Text;
using GreeACLocalServer.Device.Services;

namespace GreeACLocalServer.Device.Tests;

/// <summary>
/// <see cref="PrefixedReadStream"/> must replay the peeked prefix byte(s) and then
/// forward transparently, so a <see cref="StreamReader"/> sees the original stream.
/// </summary>
public class PrefixedReadStreamTests
{
    [Fact]
    public async Task ReplaysPrefixThenForwardsToInner()
    {
        using var inner = new MemoryStream(Encoding.ASCII.GetBytes("world"));
        using var stream = new PrefixedReadStream(Encoding.ASCII.GetBytes("he"), inner);
        using var reader = new StreamReader(stream);

        Assert.Equal("heworld", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task ServesBytes_AcrossPrefixInnerBoundary_OneAtATime()
    {
        using var inner = new MemoryStream([3, 4]);
        using var stream = new PrefixedReadStream(new byte[] { 1, 2 }, inner);

        var seen = new List<int>();
        var buffer = new byte[1];
        int n;
        while ((n = await stream.ReadAsync(buffer)) > 0)
        {
            seen.Add(buffer[0]);
        }

        Assert.Equal([1, 2, 3, 4], seen);
    }
}
