using Sunfish.Blocks.Docs.Services;
using Xunit;

namespace Sunfish.Blocks.Docs.Tests;

public class MimeSnifferTests
{
    private static byte[] B(params int[] vals) => vals.Select(v => (byte)v).ToArray();

    [Fact]
    public void Sniff_EmptyInput_ReturnsUnknown()
    {
        Assert.Equal(MimeSniffer.UnknownMime, MimeSniffer.Sniff(ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void Sniff_Pdf_MatchesPercentPDFMagic()
    {
        // "%PDF-1.4..."
        var pdf = "%PDF-1.4\n%abc"u8.ToArray();
        Assert.Equal("application/pdf", MimeSniffer.Sniff(pdf));
    }

    [Fact]
    public void Sniff_Png_MatchesEightByteMagic()
    {
        var png = B(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D);
        Assert.Equal("image/png", MimeSniffer.Sniff(png));
    }

    [Fact]
    public void Sniff_Jpeg_MatchesFFD8FFMagic()
    {
        var jpeg = B(0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10);
        Assert.Equal("image/jpeg", MimeSniffer.Sniff(jpeg));
    }

    [Fact]
    public void Sniff_Gif_MatchesGif89aMagic()
    {
        var gif = "GIF89a..."u8.ToArray();
        Assert.Equal("image/gif", MimeSniffer.Sniff(gif));
    }

    [Fact]
    public void Sniff_Webp_MatchesRiffWebpMagic()
    {
        // "RIFF????WEBP"
        var webp = "RIFFsizeWEBPdata"u8.ToArray();
        Assert.Equal("image/webp", MimeSniffer.Sniff(webp));
    }

    [Fact]
    public void Sniff_Heic_MatchesFtypHeicAtOffset4()
    {
        var heic = "????ftypheic..."u8.ToArray();
        Assert.Equal("image/heic", MimeSniffer.Sniff(heic));
    }

    [Fact]
    public void Sniff_Zip_MatchesPKMagic()
    {
        var zip = B(0x50, 0x4B, 0x03, 0x04, 0x14, 0x00);
        Assert.Equal("application/zip", MimeSniffer.Sniff(zip));
    }

    [Fact]
    public void Sniff_WindowsExe_DetectedAsXMsdownload()
    {
        var exe = "MZ\x90\x00"u8.ToArray();
        Assert.Equal("application/x-msdownload", MimeSniffer.Sniff(exe));
    }

    [Fact]
    public void Sniff_Elf_DetectedAsXExecutable()
    {
        // Use a byte array — C#'s `\x` escape is greedy (up to 4 hex digits),
        // so "\x7FELF" would parse as \x7FE + LF, not the ELF magic.
        var elf = B(0x7F, 0x45, 0x4C, 0x46);
        Assert.Equal("application/x-executable", MimeSniffer.Sniff(elf));
    }

    [Fact]
    public void Sniff_Shebang_DetectedAsShellScript()
    {
        var sh = "#!/bin/sh\necho hi"u8.ToArray();
        Assert.Equal("text/x-shellscript", MimeSniffer.Sniff(sh));
    }

    [Fact]
    public void Sniff_Html_DetectedAsTextHtml()
    {
        var html = "<!doctype html><html><body>hi</body></html>"u8.ToArray();
        Assert.Equal("text/html", MimeSniffer.Sniff(html));
    }

    [Fact]
    public void Sniff_Svg_DetectedAsImageSvgXml()
    {
        var svg = "<?xml version=\"1.0\"?><svg width=\"100\"></svg>"u8.ToArray();
        Assert.Equal("image/svg+xml", MimeSniffer.Sniff(svg));
    }

    [Fact]
    public void Sniff_PlainTextWithoutMagic_ReturnsUnknownMime()
    {
        // Bytes without any recognized magic — falls through to
        // application/octet-stream. Council requirement: detection
        // failure → deny, so the policy gate rejects this unless the
        // tenant explicitly whitelists the unknown MIME.
        var plain = "just some text"u8.ToArray();
        Assert.Equal(MimeSniffer.UnknownMime, MimeSniffer.Sniff(plain));
    }
}
