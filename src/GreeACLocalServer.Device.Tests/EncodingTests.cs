using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace GreeACLocalServer.Device.Tests;

/// <summary>
/// Guards WP-02 finding F3: the device stream must not emit a UTF-8 BOM on the
/// first response. Mirrors the encoding used by
/// <c>SocketHandlerService.HandleClientAsync</c>.
/// </summary>
public class EncodingTests
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    [Fact]
    public void FirstWriteLine_DoesNotStartWithBom()
    {
        using var ms = new MemoryStream();

        using (var writer = new StreamWriter(ms, Utf8NoBom) { AutoFlush = false, NewLine = "\n" })
        {
            writer.WriteLine("{\"t\":\"pack\"}");
        }

        var bytes = ms.ToArray();

        Assert.NotEmpty(bytes);
        Assert.NotEqual(0xEF, bytes[0]);
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF,
            "Output must not begin with a UTF-8 BOM (EF BB BF).");
        Assert.Equal((byte)'{', bytes[0]);
        Assert.Equal((byte)'\n', bytes[^1]);
    }

    [Fact]
    public void Reader_StillStripsIncomingBom()
    {
        var payload = new byte[] { 0xEF, 0xBB, 0xBF }
            .Concat(Encoding.UTF8.GetBytes("{\"hello\":1}\n"))
            .ToArray();

        using var ms = new MemoryStream(payload);
        using var reader = new StreamReader(ms, Utf8NoBom, detectEncodingFromByteOrderMarks: true);

        var line = reader.ReadLine();

        Assert.Equal("{\"hello\":1}", line);
    }
}
