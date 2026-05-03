using System;
using System.Text.Json.Serialization;

namespace Sunfish.Foundation.Recovery;

/// <summary>
/// ADR 0046-A2 envelope for an at-rest encrypted scalar field
/// (TIN, SSN, payout-account number, signature payload, etc.).
/// </summary>
/// <remarks>
/// <para>
/// W#32 Phase 1 deliverable per ADR 0046-A2.1: net-new value type only,
/// no DI changes, no behavior. The encryptor / decryptor reference
/// implementations land in Phase 2 (ADR 0046-A4). Phase 1 deliberately
/// does not validate ranges — the structural contract is "three opaque
/// fields"; range invariants (e.g. <c>KeyVersion &gt;= 1</c>) are enforced
/// by the decryptor in Phase 2 per amendment A5.5.
/// </para>
/// <para>
/// <b>JSON shape</b> (via <see cref="EncryptedFieldJsonConverter"/>):
/// <c>{ "ct": "&lt;base64url&gt;", "nonce": "&lt;base64url&gt;", "kv": &lt;int&gt; }</c>.
/// Base64url (no padding) keeps the on-the-wire form URL-safe and avoids
/// quoted '+' / '/' / '=' characters that complicate downstream tooling.
/// </para>
/// <para>
/// <b>What this is NOT.</b> It is not a persistence column type, not a
/// PII-classification marker, and not a key-management primitive. It is
/// only the byte-shape that travels with an encrypted field.
/// </para>
/// </remarks>
[JsonConverter(typeof(EncryptedFieldJsonConverter))]
public readonly record struct EncryptedField(
    ReadOnlyMemory<byte> Ciphertext,
    ReadOnlyMemory<byte> Nonce,
    int KeyVersion)
{
    /// <summary>
    /// Returns a redacted summary that intentionally omits ciphertext and
    /// nonce bytes. Phase 1 invariant: <c>ToString()</c> must never expose
    /// the field contents — log-leak defense in depth.
    /// </summary>
    public override string ToString()
        => $"EncryptedField(KeyVersion={KeyVersion}, CiphertextLength={Ciphertext.Length}, NonceLength={Nonce.Length})";
}
