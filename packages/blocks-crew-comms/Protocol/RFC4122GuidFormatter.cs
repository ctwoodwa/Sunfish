using System;
using System.Buffers;
using System.Buffers.Binary;
using MessagePack;
using MessagePack.Formatters;

namespace Sunfish.Blocks.CrewComms.Protocol;

/// <summary>
/// MessagePack formatter that always serializes <see cref="Guid"/> in
/// RFC 4122 big-endian byte order (16 bytes). The default <c>Guid.ToByteArray()</c>
/// uses mixed-endian (little-endian for Data1/Data2/Data3, big-endian for Data4)
/// which is a Microsoft-only convention and would not interop with non-.NET peers.
/// </summary>
/// <remarks>
/// Per ADR 0076 wire protocol — all UUID fields (TenantId, MessageId, etc.) are
/// 16-byte RFC 4122 big-endian. Cohort precedent: PR #506 transcript-hash binding.
/// </remarks>
public sealed class RFC4122GuidFormatter : IMessagePackFormatter<Guid>
{
    /// <summary>Singleton formatter instance.</summary>
    public static readonly RFC4122GuidFormatter Instance = new();

    private RFC4122GuidFormatter() { }

    /// <inheritdoc />
    public void Serialize(ref MessagePackWriter writer, Guid value, MessagePackSerializerOptions options)
    {
        var buf = new byte[16];
        WriteBigEndian(buf, value);
        writer.Write(buf);
    }

    /// <inheritdoc />
    public Guid Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        var seq = reader.ReadBytes() ?? throw new MessagePackSerializationException("Guid bytes were null.");
        if (seq.Length != 16)
            throw new MessagePackSerializationException($"Guid must be 16 bytes; got {seq.Length}.");
        Span<byte> buf = stackalloc byte[16];
        seq.CopyTo(buf);
        return ReadBigEndian(buf);
    }

    /// <summary>Writes a Guid as RFC 4122 big-endian (16 bytes) into <paramref name="dest"/>.</summary>
    public static void WriteBigEndian(Span<byte> dest, Guid value)
    {
        if (dest.Length < 16)
            throw new ArgumentException("Destination must be at least 16 bytes.", nameof(dest));

        Span<byte> ms = stackalloc byte[16];
        if (!value.TryWriteBytes(ms))
            throw new InvalidOperationException("Guid.TryWriteBytes failed unexpectedly.");

        // MS layout: Data1 (4B LE), Data2 (2B LE), Data3 (2B LE), Data4 (8B BE).
        // RFC 4122: all fields big-endian. Reverse the first three groups.
        var d1 = BinaryPrimitives.ReadUInt32LittleEndian(ms[..4]);
        var d2 = BinaryPrimitives.ReadUInt16LittleEndian(ms.Slice(4, 2));
        var d3 = BinaryPrimitives.ReadUInt16LittleEndian(ms.Slice(6, 2));
        BinaryPrimitives.WriteUInt32BigEndian(dest[..4], d1);
        BinaryPrimitives.WriteUInt16BigEndian(dest.Slice(4, 2), d2);
        BinaryPrimitives.WriteUInt16BigEndian(dest.Slice(6, 2), d3);
        ms.Slice(8, 8).CopyTo(dest.Slice(8, 8));
    }

    /// <summary>Parses a 16-byte RFC 4122 big-endian span into a <see cref="Guid"/>.</summary>
    public static Guid ReadBigEndian(ReadOnlySpan<byte> source)
    {
        if (source.Length < 16)
            throw new ArgumentException("Source must be at least 16 bytes.", nameof(source));

        Span<byte> ms = stackalloc byte[16];
        var d1 = BinaryPrimitives.ReadUInt32BigEndian(source[..4]);
        var d2 = BinaryPrimitives.ReadUInt16BigEndian(source.Slice(4, 2));
        var d3 = BinaryPrimitives.ReadUInt16BigEndian(source.Slice(6, 2));
        BinaryPrimitives.WriteUInt32LittleEndian(ms[..4], d1);
        BinaryPrimitives.WriteUInt16LittleEndian(ms.Slice(4, 2), d2);
        BinaryPrimitives.WriteUInt16LittleEndian(ms.Slice(6, 2), d3);
        source.Slice(8, 8).CopyTo(ms.Slice(8, 8));
        return new Guid(ms);
    }
}

/// <summary>
/// MessagePack resolver that surfaces <see cref="RFC4122GuidFormatter"/> for
/// <see cref="Guid"/> and falls through to MessagePack's <c>StandardResolver</c>
/// for everything else.
/// </summary>
public sealed class CrewCommsResolver : IFormatterResolver
{
    /// <summary>Singleton resolver instance.</summary>
    public static readonly CrewCommsResolver Instance = new();

    private static readonly MessagePackSerializerOptions s_options =
        MessagePackSerializerOptions.Standard.WithResolver(Instance);

    /// <summary>Pre-built options instance using this resolver.</summary>
    public static MessagePackSerializerOptions Options => s_options;

    private CrewCommsResolver() { }

    /// <inheritdoc />
    public IMessagePackFormatter<T>? GetFormatter<T>()
    {
        if (typeof(T) == typeof(Guid))
            return (IMessagePackFormatter<T>)(object)RFC4122GuidFormatter.Instance;
        return MessagePack.Resolvers.StandardResolver.Instance.GetFormatter<T>();
    }
}
