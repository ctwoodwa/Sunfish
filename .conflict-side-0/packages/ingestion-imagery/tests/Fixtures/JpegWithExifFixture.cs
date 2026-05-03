namespace Sunfish.Ingestion.Imagery.Tests.Fixtures;

/// <summary>
/// Synthetic byte fixtures used by the extractor tests. No real JPEG with EXIF GPS/DateTime is
/// committed in this session — coverage of GPS / timestamp / camera extraction against a real
/// fixture JPEG is a parking-lot item for a follow-up PR (track alongside the Phase C backlog).
/// </summary>
internal static class JpegWithExifFixture
{
    /// <summary>
    /// Minimal well-formed JPEG body: SOI (0xFFD8), trivial APP0/JFIF header, and EOI (0xFFD9).
    /// MetadataExtractor will parse this as a JPEG with no EXIF and therefore no populated fields.
    /// </summary>
    public static byte[] MinimalJpeg() => new byte[]
    {
        // SOI
        0xFF, 0xD8,
        // APP0 marker
        0xFF, 0xE0,
        // APP0 length (16 bytes)
        0x00, 0x10,
        // "JFIF\0"
        0x4A, 0x46, 0x49, 0x46, 0x00,
        // version 1.01
        0x01, 0x01,
        // units = 0 (no units), xDensity = 1, yDensity = 1
        0x00, 0x00, 0x01, 0x00, 0x01,
        // thumbnail width/height = 0/0
        0x00, 0x00,
        // EOI
        0xFF, 0xD9,
    };

    /// <summary>
    /// Minimal PNG signature + IHDR chunk for a 1x1 image + IEND. Not strictly complete but
    /// MetadataExtractor should recognise the signature and produce a <c>PngDirectory</c>.
    /// </summary>
    public static byte[] MinimalPng() => new byte[]
    {
        // PNG signature
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        // IHDR length = 13
        0x00, 0x00, 0x00, 0x0D,
        // "IHDR"
        0x49, 0x48, 0x44, 0x52,
        // width = 1
        0x00, 0x00, 0x00, 0x01,
        // height = 1
        0x00, 0x00, 0x00, 0x01,
        // bit depth = 8, colour type = 2 (RGB), compression = 0, filter = 0, interlace = 0
        0x08, 0x02, 0x00, 0x00, 0x00,
        // IHDR CRC (placeholder — MetadataExtractor may or may not validate)
        0x90, 0x77, 0x53, 0xDE,
        // IEND length = 0
        0x00, 0x00, 0x00, 0x00,
        // "IEND"
        0x49, 0x45, 0x4E, 0x44,
        // IEND CRC
        0xAE, 0x42, 0x60, 0x82,
    };

    /// <summary>Arbitrary four bytes that are not a valid image file header.</summary>
    public static byte[] CorruptBytes() => new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
}
