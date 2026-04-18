using Sunfish.Ingestion.Imagery.Metadata;
using Sunfish.Ingestion.Imagery.Tests.Fixtures;
using Xunit;

namespace Sunfish.Ingestion.Imagery.Tests;

/// <summary>
/// Coverage of <see cref="ExifImageryMetadataExtractor"/> against synthetic streams. GPS /
/// timestamp / camera extraction against a real JPEG fixture is deferred (parking-lot item).
/// </summary>
public class ExifImageryMetadataExtractorTests
{
    private readonly ExifImageryMetadataExtractor _sut = new();

    [Fact]
    public void Extract_EmptyStream_ReturnsAllNullFields()
    {
        using var stream = new MemoryStream(Array.Empty<byte>());

        var metadata = _sut.Extract(stream);

        Assert.Null(metadata.GpsLatitude);
        Assert.Null(metadata.GpsLongitude);
        Assert.Null(metadata.CapturedUtc);
        Assert.Null(metadata.CameraMake);
        Assert.Null(metadata.CameraModel);
        Assert.Null(metadata.FocalLengthMm);
        Assert.Null(metadata.WidthPx);
        Assert.Null(metadata.HeightPx);
    }

    [Fact]
    public void Extract_CorruptBytes_ReturnsAllNullFields()
    {
        using var stream = new MemoryStream(JpegWithExifFixture.CorruptBytes());

        var metadata = _sut.Extract(stream);

        // MetadataExtractor rejects unknown formats via ImageProcessingException — extractor must swallow.
        Assert.Equal(ImageryMetadata.Empty, metadata);
    }

    [Fact]
    public void Extract_MinimalJpegNoExif_ReturnsAllNullGpsAndCameraFields()
    {
        using var stream = new MemoryStream(JpegWithExifFixture.MinimalJpeg());

        var metadata = _sut.Extract(stream);

        // A valid JPEG without EXIF must yield null GPS / timestamp / camera / focal / dimension fields.
        Assert.Null(metadata.GpsLatitude);
        Assert.Null(metadata.GpsLongitude);
        Assert.Null(metadata.CapturedUtc);
        Assert.Null(metadata.CameraMake);
        Assert.Null(metadata.CameraModel);
        Assert.Null(metadata.FocalLengthMm);
    }

    [Fact]
    public void Extract_DoesNotThrow_OnExoticFormats()
    {
        // Non-image bytes (arbitrary binary noise) — must not throw.
        using var stream = new MemoryStream(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x01, 0x00, 0x01, 0x00 });

        var metadata = _sut.Extract(stream);

        Assert.NotNull(metadata);
    }

    [Fact]
    public void Extract_MinimalPng_ReturnsAllNullExifFields()
    {
        using var stream = new MemoryStream(JpegWithExifFixture.MinimalPng());

        var metadata = _sut.Extract(stream);

        // PNG has no EXIF subdirectory — GPS / timestamp / camera / focal must all be null.
        Assert.Null(metadata.GpsLatitude);
        Assert.Null(metadata.GpsLongitude);
        Assert.Null(metadata.CapturedUtc);
        Assert.Null(metadata.CameraMake);
        Assert.Null(metadata.CameraModel);
        Assert.Null(metadata.FocalLengthMm);
    }
}
