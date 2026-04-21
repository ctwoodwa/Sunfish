---
uid: library-zip-library-compress-stream
title: Zip Library — Compress Stream
description: Aspirational library doc — streaming ZIP compression capability not in current Sunfish reporting-pipeline scope.
keywords:
  - streaming compression
  - ZipArchive
  - DeflateStream
  - SharpZipLib
  - aspirational
  - ADR 0021
---

# Zip Library — Compress Stream

> **Status: Aspirational.** No Sunfish implementation yet. This page documents the
> target capability and links to the Telerik parity reference. See
> [ADR 0021](xref:adr-0021-reporting-pipeline-policy) for the reporting pipeline policy.

## Overview

Streaming compression is the API pattern where archive entries are written and
read as **streams** — without buffering the full entry's bytes in memory and
without needing to know the entry's final decompressed size up front.

The target write-side surface:

- Open a target stream (a file, a network stream, or an HTTP response body).
- Open a new entry on the archive by name, receiving a `Stream` positioned at
  the start of the entry's compressed content.
- Write entry content to that stream incrementally; the library emits
  deflate-compressed bytes to the target as they are produced.
- Close the entry stream; the library writes the local-file-header CRC32 and
  size fields (or a data descriptor after the entry, if the entry was emitted
  with streaming mode).
- Repeat for each entry, then close the archive — the library writes the
  central directory and end-of-central-directory record.

The target read-side surface:

- Open a source stream on an existing archive.
- Enumerate entries.
- Open any entry's content as a decompressing `Stream`.
- Read content incrementally; decompression happens as bytes are consumed.

Memory characteristics:

- Peak working set is independent of entry size or total archive size.
- One in-flight deflate window per open entry (tens of KB).
- The central directory is buffered until archive close (proportional to the
  entry count, not the entry contents).

This matches the OpenXML writer pattern (XLSX / DOCX / PPTX are ZIP archives of
XML parts) and is how streaming XLSX and streaming document export pipelines
compose on top of ZIP.

## Telerik parity reference

- Telerik package: `Telerik.Windows.Zip` — the `CompressedStream` and
  `ZipArchive` / `ZipPackage` stream-mode APIs.
- Docs: <https://docs.telerik.com/devtools/document-processing/libraries/radziplibrary/features/compressed-stream>

## Sunfish plans

No Sunfish equivalent exists today and none is planned in
[ADR 0021](xref:adr-0021-reporting-pipeline-policy); see
[Overview](overview.md) for the full rationale.

For streaming compression in pure-OSS today:

- **`System.IO.Compression.ZipArchive`** in `ZipArchiveMode.Create` mode, with
  `ZipArchiveEntry.Open()` returning a write stream, gives a clean streaming
  write-side surface without buffering entry contents.
- **`System.IO.Compression.DeflateStream`** and `GZipStream` are the primitives
  underneath for raw deflate / gzip without the ZIP container.
- **SharpZipLib** `ZipOutputStream` / `ZipInputStream` are the streaming-first
  equivalents, with added password and AES support.

A `Sunfish.Foundation.Zip` streaming contract is not in scope; the streaming
XLSX, streaming DOCX, and streaming PPTX contracts in
[ADR 0021](xref:adr-0021-reporting-pipeline-policy)'s deferred second-phase
scope would each use their adapter's native ZIP stream layer directly rather
than routing through a Sunfish-owned zip abstraction.

## Related

- [Overview](overview.md)
