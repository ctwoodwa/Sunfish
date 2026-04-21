---
uid: library-zip-library-overview
title: Zip Library — Overview
description: aspirational library doc
---

# Zip Library — Overview

> **Status: Aspirational.** No Sunfish implementation yet. This page documents the
> target capability and links to the Telerik parity reference. See
> [ADR 0021](xref:adr-0021-reporting-pipeline-policy) for the reporting pipeline policy.

## Overview

The zip library covers programmatic creation and reading of ZIP archives —
reading entries from an existing archive, adding files and directories, and
producing an archive as a byte stream without a temporary file on disk.

The target surface:

- **Archive creation** — open a new archive on a target stream, add entries by
  name with content from a source stream or byte array, set per-entry modification
  timestamps and external attributes, close the archive to flush the central
  directory.
- **Archive reading** — open an existing archive on a source stream, enumerate
  entries by name, open an entry's decompressed content as a stream.
- **Compression levels** — `Store` (no compression), `Fastest`, `Default`, `Best`,
  per-entry.
- **Compression methods** — Deflate (always), optionally Deflate64, BZip2, and
  LZMA for archives that need it.
- **Password protection** — AES-128 / AES-256 encryption (strong encryption) and
  legacy ZipCrypto (for compatibility with old tools), per-entry password.
- **Large archives** — ZIP64 support for archives over 4 GB or with more than
  65,535 entries.
- **Streaming read and write** — entries can be added or consumed without
  buffering the full archive or entry in memory (see
  [Compress Stream](compress-stream.md)).

## Telerik parity reference

- Telerik package: Telerik Zip Library (`Telerik.Windows.Zip`).
- Docs: <https://docs.telerik.com/devtools/document-processing/libraries/radziplibrary/overview>

## Sunfish plans

No Sunfish equivalent exists today, and **no `Sunfish.Foundation.Zip` contract is
defined in [ADR 0021](xref:adr-0021-reporting-pipeline-policy)**. The reporting
pipeline policy covers document generation (PDF, XLSX, DOCX, PPTX, CSV) and does
not extend to archive compression.

For archive creation today, deployers have multiple pure-OSS options without any
Sunfish abstraction layer:

- **`System.IO.Compression`** — MIT, ships with .NET. Covers the common
  `Store` and `Deflate` cases, ZIP64, async streaming, and `ZipArchive` /
  `ZipFile` APIs. No password support.
- **SharpZipLib** — MIT. Covers Deflate, BZip2, LZMA, ZipCrypto, and AES-128 /
  AES-256 encryption.
- **SharpCompress** — MIT. Covers ZIP, 7z, RAR (read-only), TAR, and GZip.

A Sunfish-owned `IZipArchiveWriter` / `IZipArchiveReader` contract is **not** in
the currently planned scope. If a bundle-level compression surface becomes
necessary, it will be scoped as a separate ADR rather than folded into ADR 0021.

## Related

- [Overview](overview.md)
- [Compress Stream](compress-stream.md)
