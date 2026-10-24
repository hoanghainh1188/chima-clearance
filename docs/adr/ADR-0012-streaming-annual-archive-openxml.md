# ADR-0012: Stream the annual archive with DocumentFormat.OpenXml

**Status**: **Proposed** — awaiting owner ratification with the PR that implements it.
**Date**: 2026-10-24
**Relates to**: #167 (full-year ClosedXML archive exhausts memory), FR-021 / INV-12 (annual
archive; delete live data only after the archive verifies), FR-015 (Sổ B2 template layout),
`specs/01-overview.md` § 6 (Excel I/O = ClosedXML).

## Context

The annual archive job (FR-021) exports a whole calendar year of Sổ B2 rows to one `.xlsx`, then
re-opens it to check it holds at least as many declaration rows as the database before deleting
live data (INV-12). Both steps went through ClosedXML, which keeps every cell and style of the
workbook as objects: the export returned a `byte[]`, and verification loaded the file a second
time. The gate PC runs this job unattended at 01:00 on 1 February alongside the service.

Measured on a seeded year of 40 000 declarations / 60 000 vehicles (≈ 100 000 sheet rows):

| Path | Peak memory footprint | Allocated | Time |
| --- | --- | --- | --- |
| ClosedXML export + ClosedXML verify (before) | 383–396 MB | ≈ 6.6 GB | 25–39 s |
| Streaming export + streaming verify (this ADR) | ≈ 71 MB | ≈ 1.2 GB | 6–14 s |

(macOS, .NET 8 workstation GC, `/usr/bin/time -l` peak footprint, three runs each; the export
data is held in memory in both cases, so the gap is the workbook itself.)

On the ClosedXML path memory grows with the year's volume; on the streaming path the workbook
costs a constant one row at a time, and in production the data is loaded one month at a time. Two alternatives were considered:

- **Split the archive into monthly workbooks or sheets** — bounded memory with ClosedXML, but it
  changes the FR-021 archive layout (one `Số_B2_archive_{year}.xlsx` the unit opens and searches
  as one book) and the operator runbook, and still loads a month as a DOM.
- **Stream with the OpenXML SDK** — keeps what the user sees identical (same template, columns, colours, borders, number format and duplicate rule) while writing
  and reading one row at a time.

## Decision

Use **DocumentFormat.OpenXml** (the OpenXML SDK) directly, for the annual archive only:

- `OpenXmlStreamingSoB2Exporter` copies the Sổ B2 template package and rewrites its first sheet
  with `OpenXmlWriter`, one day at a time, into a `ZipArchiveMode.Create` zip on the archive file.
  Data is loaded one calendar month at a time (`ExportSoB2QueryHandler.StreamDaysAsync`).
- `FileSystemArchiveWriter.VerifyExcel` counts declaration rows with `OpenXmlReader`, one row at a
  time, on a read-only stream.
- The interactive F-13 export (`ClosedXmlSoB2Exporter`, a date range picked by an operator) stays
  on ClosedXML. A test pins the streamed archive to it cell for cell.

**No new package is added.** DocumentFormat.OpenXml is already in the build as ClosedXML's own
dependency (ClosedXML 0.104.2 → DocumentFormat.OpenXml ≥ 3.1.1 < 4.0) and already ships in the
deployment. The code now calls it directly, which is why this ADR exists (Constitution II); its
version stays governed by the ClosedXML reference rather than a second `PackageReference`.

## Consequences

- The FR-021 archive layout, path and read-only marking are unchanged; operator docs need no edit.
- The Sổ B2 layout now has two renderers. `OpenXmlStreamingSoB2ExporterTests` compares them cell
  by cell (value, type, font, fill, borders, wrap, number format, widths) and validates the output
  against the OpenXML schema, so a layout change to one that is not made to the other fails CI.
- A ClosedXML upgrade that moves DocumentFormat.OpenXml to a new major version must re-run those
  tests; the SDK's writer/reader API has been stable since 2.x.
