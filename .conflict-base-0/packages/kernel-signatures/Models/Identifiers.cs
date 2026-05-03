namespace Sunfish.Kernel.Signatures.Models;

/// <summary>Identifier for a <see cref="SignatureEvent"/>.</summary>
public readonly record struct SignatureEventId(Guid Value);

/// <summary>Identifier for a <see cref="ConsentRecord"/>.</summary>
public readonly record struct ConsentRecordId(Guid Value);

/// <summary>Identifier for a <see cref="SignatureRevocation"/> entry in the append-only revocation log.</summary>
public readonly record struct RevocationEventId(Guid Value);

/// <summary>Opaque reference to an encrypted pen-stroke blob in tenant-key-encrypted storage.</summary>
/// <param name="BlobUri">Storage-scheme URI of the blob (e.g. <c>blob:sunfish-tenant-keys/&lt;hash&gt;</c>).</param>
/// <param name="ByteCount">Size of the encrypted payload — useful for storage-budget audit + UI capacity hints.</param>
public readonly record struct PenStrokeBlobRef(string BlobUri, long ByteCount);
