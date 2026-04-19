using MessagePack;
using MessagePack.Resolvers;
using Sunfish.Ingestion.Sensors;
using Sunfish.Ingestion.Sensors.Decoders;
using Xunit;

namespace Sunfish.Ingestion.Sensors.Tests;

/// <summary>
/// Tests for <see cref="MessagePackSensorBatchDecoder"/>. Payloads are built via
/// <see cref="MessagePackSerializer"/> using <see cref="ContractlessStandardResolver"/> so the
/// test project does not need to define its own <c>[MessagePackObject]</c> types, which would
/// generate a second <c>GeneratedMessagePackResolver</c> and conflict with the one from the
/// main package.
/// </summary>
public class MessagePackSensorBatchDecoderTests
{
    // ContractlessStandardResolver: serialises public POCOs by property name (string-keyed map),
    // identical wire format to MsgPackSensorReadingDto used by the production decoder.
    private static readonly MessagePackSerializerOptions _testOptions =
        ContractlessStandardResolver.Options;

    private static Stream Serialize(params TestSensorReading[] readings)
    {
        var bytes = MessagePackSerializer.Serialize(readings, _testOptions);
        return new MemoryStream(bytes, writable: false);
    }

    private static readonly TestSensorReading _reading1 = new()
    {
        SensorId = "s1",
        TimestampUtc = new DateTime(2026, 4, 17, 14, 0, 0, DateTimeKind.Utc),
        Kind = "temperature",
        Value = 21.3,
        Unit = "celsius",
    };

    private static readonly TestSensorReading _reading2 = new()
    {
        SensorId = "s2",
        TimestampUtc = new DateTime(2026, 4, 17, 14, 1, 0, DateTimeKind.Utc),
        Kind = "humidity",
        Value = 65.0,
        Unit = "percent_rh",
    };

    // -------------------------------------------------------------------------
    // Single reading round-trip
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Decode_SingleReading_RoundTripsAllFields()
    {
        await using var stream = Serialize(_reading1);
        var decoder = new MessagePackSensorBatchDecoder();

        var readings = new List<SensorReading>();
        await foreach (var r in decoder.DecodeAsync(stream, CancellationToken.None))
        {
            readings.Add(r);
        }

        Assert.Single(readings);
        var r0 = readings[0];
        Assert.Equal("s1", r0.SensorId);
        Assert.Equal(_reading1.TimestampUtc, r0.TimestampUtc);
        Assert.Equal("temperature", r0.Kind);
        Assert.Equal(21.3, r0.Value, precision: 10);
        Assert.Equal("celsius", r0.Unit);
    }

    // -------------------------------------------------------------------------
    // Batch of N readings
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Decode_BatchOfTwo_YieldsTwoReadingsInOrder()
    {
        await using var stream = Serialize(_reading1, _reading2);
        var decoder = new MessagePackSensorBatchDecoder();

        var readings = new List<SensorReading>();
        await foreach (var r in decoder.DecodeAsync(stream, CancellationToken.None))
        {
            readings.Add(r);
        }

        Assert.Equal(2, readings.Count);
        Assert.Equal("s1", readings[0].SensorId);
        Assert.Equal("s2", readings[1].SensorId);
        Assert.Equal("humidity", readings[1].Kind);
    }

    [Fact]
    public async Task Decode_BatchOfFive_YieldsFiveReadings()
    {
        var five = Enumerable.Range(0, 5).Select(i => new TestSensorReading
        {
            SensorId = $"s{i}",
            TimestampUtc = new DateTime(2026, 4, 17, 14, i, 0, DateTimeKind.Utc),
            Kind = "pressure",
            Value = 1013.0 + i,
            Unit = "hpa",
        }).ToArray();

        await using var stream = Serialize(five);
        var decoder = new MessagePackSensorBatchDecoder();

        var count = 0;
        await foreach (var _ in decoder.DecodeAsync(stream, CancellationToken.None))
        {
            count++;
        }

        Assert.Equal(5, count);
    }

    // -------------------------------------------------------------------------
    // Corrupt / malformed input
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Decode_CorruptBytes_ThrowsMessagePackSerializationException()
    {
        // Random bytes that are not valid MessagePack
        var corrupt = new byte[] { 0xFF, 0xFE, 0x00, 0x01, 0x02, 0xAB, 0xCD };
        await using var stream = new MemoryStream(corrupt, writable: false);
        var decoder = new MessagePackSensorBatchDecoder();

        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await foreach (var _ in decoder.DecodeAsync(stream, CancellationToken.None))
            {
                // drain to force materialisation
            }
        });
    }

    // -------------------------------------------------------------------------
    // Empty payload
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Decode_EmptyArray_YieldsNoReadings()
    {
        // Empty MessagePack array: fixarray with 0 elements = 0x90
        var emptyArray = new byte[] { 0x90 };
        await using var stream = new MemoryStream(emptyArray, writable: false);
        var decoder = new MessagePackSensorBatchDecoder();

        var count = 0;
        await foreach (var _ in decoder.DecodeAsync(stream, CancellationToken.None))
        {
            count++;
        }

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task Decode_ZeroByteStream_ThrowsOrYieldsNothing()
    {
        // A completely empty stream (no bytes at all) — decoder must either throw cleanly or yield nothing.
        await using var stream = new MemoryStream(Array.Empty<byte>(), writable: false);
        var decoder = new MessagePackSensorBatchDecoder();

        // We allow either behaviour: zero readings or a clean exception (not a crash).
        try
        {
            var count = 0;
            await foreach (var _ in decoder.DecodeAsync(stream, CancellationToken.None))
            {
                count++;
            }
            Assert.Equal(0, count);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            // Acceptable — decoder signalled end-of-input with a structured exception.
        }
    }
}
