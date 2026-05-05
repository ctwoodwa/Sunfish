#!/usr/bin/env python3
"""Generate ADR 0076-A3 conformance test vectors for crew-comms wire encoding.

This tool produces deterministic, byte-stable test vectors for the three
signable / hash sites in ADR 0076 (Crew Comms — foundation-channels):

    1. HELLO signable + Ed25519 signature
       (per ADR 0076 §A1.4 row 0x01 / §A1.5 step 2)
    2. HEARTBEAT signable + Ed25519 signature
       (per ADR 0076 §A1.3 §A3 / §A1.4 row 0x02)
    3. CONFIRM transcript hash (SHA-256)
       (per ADR 0076 §A2.3 §A1 ext / §A2.5 step 9 — A1 + A2 ratified form)

The vectors are the authoritative interop reference: a conforming
implementation in any language (.NET, Swift, Kotlin, Rust, Go) MUST
reproduce these exact byte sequences when fed identical inputs. Produced
inputs are derived from short canonical phrases via SHA-256 to make the
fixture set fully reproducible without binary blob data.

Outputs:
    tools/icm/channel-test-vectors.json       — canonical JSON artifact
                                                 (committed; CI verifies)

CLI:
    python3 tools/icm/generate-channel-vectors.py            # regenerate
    python3 tools/icm/generate-channel-vectors.py --check    # exit non-zero
                                                             # if regen output
                                                             # differs from
                                                             # the committed
                                                             # JSON

Dependencies: `cryptography` (PyPI) for Ed25519 / X25519 primitives. The
script uses raw-key APIs to avoid library-specific encoding choices.

Cohort precedent:
    tools/icm/render-ledger.py — same byte-stable regen + --check pattern.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import struct
import sys
from pathlib import Path

from cryptography.hazmat.primitives.asymmetric.ed25519 import (
    Ed25519PrivateKey,
)
from cryptography.hazmat.primitives.asymmetric.x25519 import (
    X25519PrivateKey,
)
from cryptography.hazmat.primitives import serialization

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "tools" / "icm" / "channel-test-vectors.json"

# ---------------------------------------------------------------------------
# Deterministic seed derivation
# ---------------------------------------------------------------------------
#
# All test inputs are derived from short canonical phrases via SHA-256 so the
# fixture set is fully reproducible from this source file alone — no binary
# blob data, no random state.

INITIATOR_ID_PHRASE = "sunfish-channels-test-initiator-id-v1"
RESPONDER_ID_PHRASE = "sunfish-channels-test-responder-id-v1"
INITIATOR_EPHEM_PHRASE = "sunfish-channels-test-initiator-ephem-v1"
RESPONDER_EPHEM_PHRASE = "sunfish-channels-test-responder-ephem-v1"


def seed(phrase: str) -> bytes:
    """SHA-256 of a canonical phrase — 32 bytes, suitable as Ed25519 / X25519 seed."""
    return hashlib.sha256(phrase.encode("utf-8")).digest()


def ed25519_keypair(phrase: str) -> tuple[bytes, bytes, Ed25519PrivateKey]:
    """Returns (raw_private_seed[32], raw_public[32], signing_key) for a phrase."""
    sk_seed = seed(phrase)
    sk = Ed25519PrivateKey.from_private_bytes(sk_seed)
    pk = sk.public_key().public_bytes(
        encoding=serialization.Encoding.Raw,
        format=serialization.PublicFormat.Raw,
    )
    return sk_seed, pk, sk


def x25519_keypair(phrase: str) -> tuple[bytes, bytes]:
    """Returns (raw_private[32], raw_public[32]) for an X25519 ephemeral phrase."""
    sk_seed = seed(phrase)
    sk = X25519PrivateKey.from_private_bytes(sk_seed)
    pk = sk.public_key().public_bytes(
        encoding=serialization.Encoding.Raw,
        format=serialization.PublicFormat.Raw,
    )
    return sk_seed, pk


# ---------------------------------------------------------------------------
# Canonical encoding primitives
# ---------------------------------------------------------------------------


def uint32be(value: int) -> bytes:
    """4-byte big-endian unsigned integer per ADR 0076 §A4."""
    return struct.pack(">I", value)


def int64be(value: int) -> bytes:
    """8-byte big-endian signed integer per ADR 0076 §A4 (HEARTBEAT timestamp)."""
    return struct.pack(">q", value)


def hello_signable(
    ephem_pub: bytes,
    id_pub: bytes,
    tenant_value: str,
    presence_caps: int,
) -> bytes:
    """HELLO Ed25519 signable per ADR 0076 §A1.4 row 0x01 + §A1.5 step 2.

    signable = ephemeralPublicKey[32]
            || identityPublicKey[32]
            || uint32BE(len(tenantBytes)) || tenantBytes
            || presence.caps[1]
    """
    assert len(ephem_pub) == 32
    assert len(id_pub) == 32
    assert 0 <= presence_caps <= 0xFF
    tenant_bytes = tenant_value.encode("utf-8")
    return (
        ephem_pub
        + id_pub
        + uint32be(len(tenant_bytes))
        + tenant_bytes
        + bytes([presence_caps])
    )


def heartbeat_signable(
    peer_id_raw: bytes,
    tenant_value: str,
    caps: int,
    timestamp_ms: int,
) -> bytes:
    """HEARTBEAT Ed25519 signable per ADR 0076 §A1.3 §A3 / §A1.4 row 0x02.

    signable = peerId_raw[32]
            || uint32BE(len(tenantBytes)) || tenantBytes
            || caps[1]
            || timestamp_BE[8]

    NOTE: peerId_raw is `PrincipalId.AsSpan()` (32 raw Ed25519 pubkey bytes),
    NOT `UTF8.GetBytes(PeerId.Value)` (43 base64url chars). See §A1.3 §A3.
    """
    assert len(peer_id_raw) == 32
    assert 0 <= caps <= 0xFF
    tenant_bytes = tenant_value.encode("utf-8")
    return (
        peer_id_raw
        + uint32be(len(tenant_bytes))
        + tenant_bytes
        + bytes([caps])
        + int64be(timestamp_ms)
    )


def confirm_transcript_input(
    ephem_a: bytes,
    id_a: bytes,
    ephem_b: bytes,
    id_b: bytes,
    tenant_value: str,
    invite_caps: int,
    negotiated_cap: int,
    presence_caps_a: int,
    presence_caps_b: int,
) -> bytes:
    """CONFIRM transcript-hash SHA-256 input per ADR 0076 §A2.3 §A1 ext / §A2.5 step 9.

    A1 + A2 ratified canonical form:
        SHA-256(
            ephemA[32] || idA[32] || ephemB[32] || idB[32]
         || uint32BE(len(tenantBytes)) || tenantBytes
         || inviteCaps[1]              -- A2 addition
         || negotiatedCap[1]           -- ACCEPT.capability
         || presenceCapsA[1]           -- A1 addition
         || presenceCapsB[1])          -- A1 addition

    Total: 32 + 32 + 32 + 32 + 4 + len(tenantBytes) + 1 + 1 + 1 + 1
         = 136 + len(tenantBytes) bytes.
    """
    for buf in (ephem_a, id_a, ephem_b, id_b):
        assert len(buf) == 32
    for cap in (invite_caps, negotiated_cap, presence_caps_a, presence_caps_b):
        assert 0 <= cap <= 0xFF
    tenant_bytes = tenant_value.encode("utf-8")
    return (
        ephem_a
        + id_a
        + ephem_b
        + id_b
        + uint32be(len(tenant_bytes))
        + tenant_bytes
        + bytes([invite_caps])
        + bytes([negotiated_cap])
        + bytes([presence_caps_a])
        + bytes([presence_caps_b])
    )


# ---------------------------------------------------------------------------
# Vector authoring
# ---------------------------------------------------------------------------


def hex_(b: bytes) -> str:
    return b.hex()


def build_vectors() -> dict:
    init_seed, init_pub, init_sk = ed25519_keypair(INITIATOR_ID_PHRASE)
    resp_seed, resp_pub, resp_sk = ed25519_keypair(RESPONDER_ID_PHRASE)
    init_eph_seed, init_eph_pub = x25519_keypair(INITIATOR_EPHEM_PHRASE)
    resp_eph_seed, resp_eph_pub = x25519_keypair(RESPONDER_EPHEM_PHRASE)

    # Edge-case tenant strings.
    tenant_normal = "tenant-001-acme"
    tenant_empty = ""
    tenant_short = "a"
    tenant_max = "x" * 63                # 63-byte ASCII (longest "real" tenant)
    tenant_utf8 = "tenant-é-ünïcödë"     # multi-byte UTF-8

    # Fixed sample timestamp: 2025-01-01T00:00:00Z in unix-ms.
    timestamp_ms = 1_735_689_600_000

    # Capability bitmask values (matches ChannelCapability enum).
    cap_text = 0x01
    cap_audio = 0x02
    cap_video = 0x04
    cap_all = 0x07  # text | audio | video

    vectors: dict[str, object] = {
        "schema_version": 1,
        "adr_reference": "0076-A3",
        "domain_separator": None,
        "ratified_form": "A1+A2 (no domain-separator; F2 deferred to a later amendment)",
        "fixed_inputs": {
            "initiator_identity_seed_hex": hex_(init_seed),
            "initiator_identity_pubkey_hex": hex_(init_pub),
            "responder_identity_seed_hex": hex_(resp_seed),
            "responder_identity_pubkey_hex": hex_(resp_pub),
            "initiator_x25519_seed_hex": hex_(init_eph_seed),
            "initiator_x25519_pubkey_hex": hex_(init_eph_pub),
            "responder_x25519_seed_hex": hex_(resp_eph_seed),
            "responder_x25519_pubkey_hex": hex_(resp_eph_pub),
            "sample_message_id": "00000000-0000-4000-8000-000000000001",
            "sample_timestamp_unix_ms": timestamp_ms,
        },
        "seed_provenance": {
            "rule": (
                "Each Ed25519 / X25519 seed is SHA-256(<canonical phrase>) so the "
                "fixture set is fully reproducible from source without binary blob data."
            ),
            "phrases": {
                "initiator_identity": INITIATOR_ID_PHRASE,
                "responder_identity": RESPONDER_ID_PHRASE,
                "initiator_ephemeral": INITIATOR_EPHEM_PHRASE,
                "responder_ephemeral": RESPONDER_EPHEM_PHRASE,
            },
            "warning": (
                "These keys are deterministic test fixtures. They MUST NEVER be "
                "used to sign production frames or stored in any production roster."
            ),
        },
        "vectors": [],
    }

    vlist: list = vectors["vectors"]  # type: ignore[assignment]

    # --- HELLO signables -----------------------------------------------------
    h1_signable = hello_signable(init_eph_pub, init_pub, tenant_normal, cap_all)
    h1_sig = init_sk.sign(h1_signable)
    vlist.append(
        {
            "id": "V1",
            "kind": "HELLO",
            "description": (
                "HELLO signable + Ed25519 signature; initiator identity, "
                "tenant 'tenant-001-acme', presence.caps=0x07 (text+audio+video)."
            ),
            "inputs": {
                "ephemeral_public_key_hex": hex_(init_eph_pub),
                "identity_public_key_hex": hex_(init_pub),
                "tenant_id_value": tenant_normal,
                "tenant_bytes_hex": hex_(tenant_normal.encode("utf-8")),
                "tenant_bytes_length": len(tenant_normal.encode("utf-8")),
                "presence_caps": cap_all,
            },
            "expected_signable_hex": hex_(h1_signable),
            "expected_signable_length": len(h1_signable),
            "expected_signature_hex": hex_(h1_sig),
        }
    )

    h2_signable = hello_signable(init_eph_pub, init_pub, tenant_empty, cap_text)
    h2_sig = init_sk.sign(h2_signable)
    vlist.append(
        {
            "id": "V2",
            "kind": "HELLO",
            "description": (
                "HELLO signable + Ed25519 signature; zero-length tenant edge case "
                "(uint32BE(0) length prefix with no tenant bytes following)."
            ),
            "inputs": {
                "ephemeral_public_key_hex": hex_(init_eph_pub),
                "identity_public_key_hex": hex_(init_pub),
                "tenant_id_value": tenant_empty,
                "tenant_bytes_hex": "",
                "tenant_bytes_length": 0,
                "presence_caps": cap_text,
            },
            "expected_signable_hex": hex_(h2_signable),
            "expected_signable_length": len(h2_signable),
            "expected_signature_hex": hex_(h2_sig),
        }
    )

    h3_signable = hello_signable(init_eph_pub, init_pub, tenant_utf8, cap_text | cap_audio)
    h3_sig = init_sk.sign(h3_signable)
    vlist.append(
        {
            "id": "V3",
            "kind": "HELLO",
            "description": (
                "HELLO signable + Ed25519 signature; UTF-8 multi-byte tenant "
                "('tenant-é-ünïcödë' — 21 UTF-8 bytes), presence.caps=0x03."
            ),
            "inputs": {
                "ephemeral_public_key_hex": hex_(init_eph_pub),
                "identity_public_key_hex": hex_(init_pub),
                "tenant_id_value": tenant_utf8,
                "tenant_bytes_hex": hex_(tenant_utf8.encode("utf-8")),
                "tenant_bytes_length": len(tenant_utf8.encode("utf-8")),
                "presence_caps": cap_text | cap_audio,
            },
            "expected_signable_hex": hex_(h3_signable),
            "expected_signable_length": len(h3_signable),
            "expected_signature_hex": hex_(h3_sig),
        }
    )

    # --- HEARTBEAT signables -------------------------------------------------
    hb1_signable = heartbeat_signable(init_pub, tenant_normal, cap_text, timestamp_ms)
    hb1_sig = init_sk.sign(hb1_signable)
    vlist.append(
        {
            "id": "V4",
            "kind": "HEARTBEAT",
            "description": (
                "HEARTBEAT signable + Ed25519 signature; initiator peerId raw "
                "(PrincipalId.AsSpan() — NOT base64url string), "
                "tenant 'tenant-001-acme', caps=0x01 (text), "
                "timestamp=1735689600000 (2025-01-01T00:00:00Z UTC)."
            ),
            "inputs": {
                "peer_id_raw_hex": hex_(init_pub),
                "tenant_id_value": tenant_normal,
                "tenant_bytes_hex": hex_(tenant_normal.encode("utf-8")),
                "tenant_bytes_length": len(tenant_normal.encode("utf-8")),
                "caps": cap_text,
                "timestamp_unix_ms": timestamp_ms,
            },
            "expected_signable_hex": hex_(hb1_signable),
            "expected_signable_length": len(hb1_signable),
            "expected_signature_hex": hex_(hb1_sig),
        }
    )

    hb2_signable = heartbeat_signable(resp_pub, tenant_max, cap_all, timestamp_ms)
    hb2_sig = resp_sk.sign(hb2_signable)
    vlist.append(
        {
            "id": "V5",
            "kind": "HEARTBEAT",
            "description": (
                "HEARTBEAT signable + Ed25519 signature; responder peerId raw, "
                "63-byte ASCII tenant (max practical real tenant size), "
                "caps=0x07 (all), timestamp=1735689600000."
            ),
            "inputs": {
                "peer_id_raw_hex": hex_(resp_pub),
                "tenant_id_value": tenant_max,
                "tenant_bytes_hex": hex_(tenant_max.encode("utf-8")),
                "tenant_bytes_length": len(tenant_max.encode("utf-8")),
                "caps": cap_all,
                "timestamp_unix_ms": timestamp_ms,
            },
            "expected_signable_hex": hex_(hb2_signable),
            "expected_signable_length": len(hb2_signable),
            "expected_signature_hex": hex_(hb2_sig),
        }
    )

    hb3_signable = heartbeat_signable(init_pub, tenant_short, cap_text, timestamp_ms)
    hb3_sig = init_sk.sign(hb3_signable)
    vlist.append(
        {
            "id": "V6",
            "kind": "HEARTBEAT",
            "description": (
                "HEARTBEAT signable + Ed25519 signature; 1-byte tenant 'a' "
                "(boundary case for the uint32BE length prefix)."
            ),
            "inputs": {
                "peer_id_raw_hex": hex_(init_pub),
                "tenant_id_value": tenant_short,
                "tenant_bytes_hex": hex_(tenant_short.encode("utf-8")),
                "tenant_bytes_length": len(tenant_short.encode("utf-8")),
                "caps": cap_text,
                "timestamp_unix_ms": timestamp_ms,
            },
            "expected_signable_hex": hex_(hb3_signable),
            "expected_signable_length": len(hb3_signable),
            "expected_signature_hex": hex_(hb3_sig),
        }
    )

    # --- CONFIRM transcript hashes -------------------------------------------
    # Use BOTH peers' ephemerals + identities; tenant + caps fields.
    invite_caps = cap_all          # initiator offered text+audio+video
    negotiated_cap = cap_text      # ACCEPT picked text-only
    presence_caps_init = cap_all   # initiator's HELLO.presence.caps
    presence_caps_resp = cap_text | cap_audio  # responder's HELLO.presence.caps = 0x03

    t1_input = confirm_transcript_input(
        init_eph_pub, init_pub, resp_eph_pub, resp_pub,
        tenant_normal, invite_caps, negotiated_cap, presence_caps_init, presence_caps_resp,
    )
    t1_hash = hashlib.sha256(t1_input).digest()
    vlist.append(
        {
            "id": "V7",
            "kind": "CONFIRM_TRANSCRIPT",
            "description": (
                "CONFIRM transcript-hash; A1+A2 ratified canonical form; "
                "tenant 'tenant-001-acme'; inviteCaps=0x07; negotiatedCap=0x01; "
                "presenceCapsA=0x07; presenceCapsB=0x03."
            ),
            "inputs": {
                "initiator_ephemeral_pubkey_hex": hex_(init_eph_pub),
                "initiator_identity_pubkey_hex": hex_(init_pub),
                "responder_ephemeral_pubkey_hex": hex_(resp_eph_pub),
                "responder_identity_pubkey_hex": hex_(resp_pub),
                "tenant_id_value": tenant_normal,
                "tenant_bytes_hex": hex_(tenant_normal.encode("utf-8")),
                "tenant_bytes_length": len(tenant_normal.encode("utf-8")),
                "invite_capabilities": invite_caps,
                "negotiated_capability": negotiated_cap,
                "presence_caps_initiator": presence_caps_init,
                "presence_caps_responder": presence_caps_resp,
            },
            "expected_input_hex": hex_(t1_input),
            "expected_input_length": len(t1_input),
            "expected_sha256_hex": hex_(t1_hash),
        }
    )

    t2_input = confirm_transcript_input(
        init_eph_pub, init_pub, resp_eph_pub, resp_pub,
        tenant_empty, cap_text, cap_text, cap_text, cap_text,
    )
    t2_hash = hashlib.sha256(t2_input).digest()
    vlist.append(
        {
            "id": "V8",
            "kind": "CONFIRM_TRANSCRIPT",
            "description": (
                "CONFIRM transcript-hash; zero-length tenant edge case "
                "(uint32BE(0) length-prefix with no tenant bytes); "
                "all caps=0x01."
            ),
            "inputs": {
                "initiator_ephemeral_pubkey_hex": hex_(init_eph_pub),
                "initiator_identity_pubkey_hex": hex_(init_pub),
                "responder_ephemeral_pubkey_hex": hex_(resp_eph_pub),
                "responder_identity_pubkey_hex": hex_(resp_pub),
                "tenant_id_value": tenant_empty,
                "tenant_bytes_hex": "",
                "tenant_bytes_length": 0,
                "invite_capabilities": cap_text,
                "negotiated_capability": cap_text,
                "presence_caps_initiator": cap_text,
                "presence_caps_responder": cap_text,
            },
            "expected_input_hex": hex_(t2_input),
            "expected_input_length": len(t2_input),
            "expected_sha256_hex": hex_(t2_hash),
        }
    )

    t3_input = confirm_transcript_input(
        init_eph_pub, init_pub, resp_eph_pub, resp_pub,
        tenant_utf8, cap_all, cap_audio, cap_all, cap_audio | cap_video,
    )
    t3_hash = hashlib.sha256(t3_input).digest()
    vlist.append(
        {
            "id": "V9",
            "kind": "CONFIRM_TRANSCRIPT",
            "description": (
                "CONFIRM transcript-hash; UTF-8 multi-byte tenant; "
                "inviteCaps=0x07 → negotiatedCap=0x02 (audio); "
                "presenceCapsA=0x07; presenceCapsB=0x06."
            ),
            "inputs": {
                "initiator_ephemeral_pubkey_hex": hex_(init_eph_pub),
                "initiator_identity_pubkey_hex": hex_(init_pub),
                "responder_ephemeral_pubkey_hex": hex_(resp_eph_pub),
                "responder_identity_pubkey_hex": hex_(resp_pub),
                "tenant_id_value": tenant_utf8,
                "tenant_bytes_hex": hex_(tenant_utf8.encode("utf-8")),
                "tenant_bytes_length": len(tenant_utf8.encode("utf-8")),
                "invite_capabilities": cap_all,
                "negotiated_capability": cap_audio,
                "presence_caps_initiator": cap_all,
                "presence_caps_responder": cap_audio | cap_video,
            },
            "expected_input_hex": hex_(t3_input),
            "expected_input_length": len(t3_input),
            "expected_sha256_hex": hex_(t3_hash),
        }
    )

    return vectors


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------


def render(vectors: dict) -> str:
    """Emit deterministic JSON. 2-space indent + trailing newline + sort_keys=False."""
    return json.dumps(vectors, indent=2, ensure_ascii=False) + "\n"


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0] if __doc__ else "")
    parser.add_argument(
        "--check",
        action="store_true",
        help="Exit non-zero if regenerated output differs from the committed JSON.",
    )
    args = parser.parse_args(argv)

    vectors = build_vectors()
    rendered = render(vectors)

    if args.check:
        existing = OUTPUT.read_text(encoding="utf-8") if OUTPUT.exists() else ""
        if existing != rendered:
            print(f"channel-test-vectors.json drift detected.", file=sys.stderr)
            print(
                f"Run: python3 tools/icm/generate-channel-vectors.py",
                file=sys.stderr,
            )
            return 1
        print("channel-test-vectors.json is up-to-date.")
        return 0

    OUTPUT.write_text(rendered, encoding="utf-8")
    print(f"wrote {OUTPUT.relative_to(ROOT)} ({len(rendered)} bytes; {len(vectors['vectors'])} vectors)")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
