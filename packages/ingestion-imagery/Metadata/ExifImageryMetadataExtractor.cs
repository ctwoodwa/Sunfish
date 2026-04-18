using System.Globalization;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Jpeg;
using MetadataExtractor.Formats.Png;

namespace Sunfish.Ingestion.Imagery.Metadata;

/// <summary>
/// <see cref="IImageryMetadataExtractor"/> implementation backed by
/// <see cref="MetadataExtractor.ImageMetadataReader"/>. Resilient against malformed input:
/// catches <see cref="ImageProcessingException"/> and <see cref="IOException"/> and returns
/// <see cref="ImageryMetadata.Empty"/>. See Sunfish Platform spec §7.5.
/// </summary>
public sealed class ExifImageryMetadataExtractor : IImageryMetadataExtractor
{
    /// <inheritdoc/>
    public ImageryMetadata Extract(Stream imageContent)
    {
        IReadOnlyList<MetadataExtractor.Directory> dirs;
        try
        {
            dirs = ImageMetadataReader.ReadMetadata(imageContent);
        }
        catch (ImageProcessingException)
        {
            return ImageryMetadata.Empty;
        }
        catch (IOException)
        {
            return ImageryMetadata.Empty;
        }

        double? lat = null, lon = null;
        DateTime? captured = null;
        string? make = null, model = null;
        double? focal = null;
        int? w = null, h = null;

        var gps = dirs.OfType<GpsDirectory>().FirstOrDefault();
        var geo = gps?.GetGeoLocation();
        if (geo is { } location)
        {
            lat = location.Latitude;
            lon = location.Longitude;
        }

        var sub = dirs.OfType<ExifSubIfdDirectory>().FirstOrDefault();
        if (sub is not null)
        {
            // EXIF DateTimeOriginal is a string "yyyy:MM:dd HH:mm:ss" with no TZ; treat as UTC.
            var dtString = sub.GetDescription(ExifDirectoryBase.TagDateTimeOriginal);
            if (!string.IsNullOrWhiteSpace(dtString)
                && DateTime.TryParseExact(
                    dtString,
                    "yyyy:MM:dd HH:mm:ss",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var parsed))
            {
                captured = parsed.Kind == DateTimeKind.Utc
                    ? parsed
                    : DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
            }

            if (sub.TryGetRational(ExifDirectoryBase.TagFocalLength, out var rat))
            {
                focal = rat.ToDouble();
            }
        }

        var ifd0 = dirs.OfType<ExifIfd0Directory>().FirstOrDefault();
        if (ifd0 is not null)
        {
            var rawMake = ifd0.GetDescription(ExifDirectoryBase.TagMake);
            var rawModel = ifd0.GetDescription(ExifDirectoryBase.TagModel);
            make = string.IsNullOrWhiteSpace(rawMake) ? null : rawMake;
            model = string.IsNullOrWhiteSpace(rawModel) ? null : rawModel;
        }

        var jpeg = dirs.OfType<JpegDirectory>().FirstOrDefault();
        if (jpeg is not null)
        {
            if (jpeg.TryGetInt32(JpegDirectory.TagImageWidth, out var jw)) w = jw;
            if (jpeg.TryGetInt32(JpegDirectory.TagImageHeight, out var jh)) h = jh;
        }

        if (w is null || h is null)
        {
            var png = dirs.OfType<PngDirectory>().FirstOrDefault();
            if (png is not null)
            {
                if (w is null && png.TryGetInt32(PngDirectory.TagImageWidth, out var pw)) w = pw;
                if (h is null && png.TryGetInt32(PngDirectory.TagImageHeight, out var ph)) h = ph;
            }
        }

        return new ImageryMetadata(lat, lon, captured, make, model, focal, w, h);
    }
}
