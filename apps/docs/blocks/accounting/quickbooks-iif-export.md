---
uid: block-accounting-quickbooks-iif-export
title: Accounting — QuickBooks IIF Export
description: How Sunfish.Blocks.Accounting produces QuickBooks-compatible IIF output from a batch of journal entries.
keywords:
  - sunfish
  - accounting
  - quickbooks
  - iif
  - intuit
  - export
  - journal-entry
---

# Accounting — QuickBooks IIF Export

## Overview

`IQuickBooksJournalEntryExporter` (default implementation `QuickBooksIifExporter`) formats a
sequence of `JournalEntry` records as a QuickBooks IIF (Intuit Interchange Format) string.
The output can be saved to a `.iif` file and imported via QuickBooks Desktop's
**File → Utilities → Import → IIF Files** dialog.

IIF was chosen over the QBO REST API for three reasons:

1. It is stable, well-documented, and SDK-free — no OAuth connection or QuickBooks SDK
   dependency is required.
2. It is consumer-generable: any Sunfish host can call `Export` and write the result to a
   file or stream without network access.
3. Predictable column positions make the output easy to verify in tests and easy to inspect
   in a plain-text editor.

## Contract

```csharp
public interface IQuickBooksJournalEntryExporter
{
    string Export(IEnumerable<JournalEntry> entries, QuickBooksExportOptions options);
}
```

### `QuickBooksExportOptions`

| Option                    | Default         | Effect |
|---------------------------|-----------------|--------|
| `AccountName`             | `"Unspecified"` | Default ACCNT value when the GL account code is unavailable. |
| `IncludeSourceReference`  | `true`          | When `true`, writes `JournalEntry.SourceReference` into the SPL-line MEMO field, prefixed with `[src]`. When `false`, SPL MEMO is left blank (apart from any per-line `Notes`). |

## Output shape

Every invocation — even for an empty batch — emits the IIF header block first:

```
!TRNS   DATE   ACCNT   MEMO   AMOUNT
!SPL    DATE   ACCNT   MEMO   AMOUNT
!ENDTRNS
```

For each journal entry, the exporter emits:

- One `TRNS` row for the first line (the primary account / transaction header), using the
  entry's `Memo` as the MEMO field.
- One `SPL` row for every remaining line, with MEMO derived from `SourceReference` (when
  `IncludeSourceReference = true`) or the line's `Notes`.
- An `ENDTRNS` terminator.

Fields are separated by tab characters. The date is serialised as `MM/dd/yyyy` — the
locale-neutral format QuickBooks requires.

### Amount sign convention

IIF uses a single-sided amount column with the sign encoding direction. The exporter
writes each line faithfully:

- **Debit lines** emit the positive `Debit` amount.
- **Credit lines** emit the negative `Credit` amount (`-credit`).

Amounts are serialised with `F2` formatting in the invariant culture (e.g. `1234.56`,
`-500.00`).

### Example

Given a two-line entry posting a $1,200 rent receipt (debit Bank, credit Rental Revenue),
the exporter emits:

```
!TRNS   DATE         ACCNT   MEMO                       AMOUNT
!SPL    DATE         ACCNT   MEMO                       AMOUNT
!ENDTRNS
TRNS    04/15/2026   1100    April rent — unit 3B       1200.00
SPL     04/15/2026   4000    [src] rent-payment:INV-7  -1200.00
ENDTRNS
```

## Behaviour guarantees

- **Empty batch**: only the header block is emitted. This is still a valid IIF file.
- **Field escaping**: tab and newline characters in `Memo`, `Notes`, and account codes are
  replaced with spaces to prevent row corruption — IIF is tab-delimited with no quoting.
- **Ordering**: entries are emitted in the order they are yielded by the input
  `IEnumerable<JournalEntry>`.

## Typical workflow

```csharp
var entries = new List<JournalEntry>();
await foreach (var entry in accounting.ListEntriesAsync(new ListEntriesQuery(from, to)))
{
    entries.Add(entry);
}

var iif = exporter.Export(entries, new QuickBooksExportOptions());
await File.WriteAllTextAsync("april-export.iif", iif);
```

## Multi-line split entries

Entries with more than two lines are supported. The first line becomes the `TRNS` row; every
subsequent line becomes an `SPL` row. Columns are preserved in input order. For a split entry
that credits rental revenue and tax payable against a single cash debit, the pinned test
output looks like:

```
TRNS   07/01/2025   1000   Split rent with tax   1100.00
SPL    07/01/2025   4000                         -1000.00
SPL    07/01/2025   2100                          -100.00
ENDTRNS
```

The test `Export_MultiLineEntry_PreservesAllLinesInOrder` verifies this behaviour directly.

## Importing into QuickBooks Desktop

1. Save the returned string to a file with a `.iif` extension (UTF-8 without BOM).
2. In QuickBooks Desktop, open **File → Utilities → Import → IIF Files**.
3. Select the file and confirm the import.

QuickBooks surfaces import errors in a dialog and writes a log file; the most common causes
of rejection are unknown account names (the default `"Unspecified"` avoids this but skips the
per-line posting) and out-of-range dates. For QBO (QuickBooks Online), use the QBO REST API
instead — IIF is Desktop-only.

## Interop caveats

- **Localisation.** `MM/dd/yyyy` and invariant-culture `F2` amounts are required by
  QuickBooks. The exporter ignores the host culture on purpose.
- **Tab delimiting.** IIF is tab-delimited with no quoting. The exporter replaces tabs,
  newlines, and carriage returns in `Memo`, `Notes`, and account-code fields with spaces so
  the row never splits.
- **Empty batch.** `Export([], options)` emits the three header rows (`!TRNS`, `!SPL`,
  `!ENDTRNS`) and nothing else. The resulting file is still valid IIF and can be imported
  with no effect.

## Related pages

- [Overview](overview.md)
- [Entity Model](entity-model.md)
- [Service Contract](service-contract.md)
