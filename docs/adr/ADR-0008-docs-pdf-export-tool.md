# ADR-0008: Dev-only tool to export the operator docs to PDF

**Status**: **Accepted** on merge — the owner ratifies this ADR by merging its PR. The tool itself
lands in a separate, later PR (Constitution II: ADR merged before the dependency).
**Date**: 2026-09-27
**Supersedes**: none
**Relates to**: ADR-0007 (offline xcopy deployment), `docs/deployment-offline.md`

## Context

Gate PCs are offline and operators are not developers. The three Vietnamese operator documents —
`docs/huong-dan-cai-dat.md` (install guide), `docs/runbook.md` (operations runbook) and
`docs/deployment-offline.md` (package internals) — ship as Markdown inside the release zip
(`release.yml`, #124). Markdown opened in Notepad is hard to follow, and the install is done from a
printed or on-screen guide, so the team hands over **PDF** editions.

The PDF of the install guide also carries something the Markdown deliberately does not: the
**initial admin password** (`admin` / seed password). It was removed from the repo docs and the zip
in #125 so that holding the USB does not reveal it; the PDF is handed over separately and marked
confidential.

The first PDFs were produced by an ad-hoc script in a throwaway session directory. Re-exporting for
the next release (or after a doc fix) means rebuilding that script from memory, and the output
already had two rendering bugs caught only by eye (fenced code inside numbered lists collapsed to
one line; a blockquote swallowed the following list items — both from a non-CommonMark parser).
The generator belongs in the repo, reviewed like everything else.

Constitution II makes `specs/01-overview.md § 6` the exclusive list of build, test and packaging
dependencies; a PDF generator is packaging tooling, hence this ADR.

## Decision

Add a **developer-only** tool at `tools/docs-pdf/`:

- `build_docs_pdf.py` — renders the three docs with **markdown-it-py** (CommonMark + GFM tables),
  wraps them in one print stylesheet (A4, Vietnamese-capable system fonts), and prints each page to
  PDF with a Chromium-based browser already on the developer's machine — Google Chrome, Chromium or
  Microsoft Edge (preinstalled on Windows 10/11) — in headless mode (`--print-to-pdf`).
- `requirements.txt` pinning `markdown-it-py` (and its single transitive dependency `mdurl`) to exact
  versions; run inside a local virtualenv. The browser is located, not installed, by the script.
- For the install guide only, the script replaces the "password is in the handed-over PDF"
  sentence with a table holding the seed password, read from the single source of truth
  `tests/B2.Server.Api.Tests/Support/AuthHttpExtensions.cs` (`SeedAdminPassword`), and adds a red
  "TÀI LIỆU MẬT" banner. The script fails if that sentence is not found, so a doc edit cannot
  silently drop the password step.
- Output goes to `tools/docs-pdf/out/` (git-ignored). **PDFs are never committed**, never attached
  to a GitHub Release, and never generated in CI — the install-guide PDF contains a credential.

Scope limits: the tool is not referenced by `B2.sln` / `B2.NoMaui.slnf`, not run by `ci.yml` or
`release.yml`, and nothing in `src/` depends on it. It adds no runtime, build or test dependency to
the product.

When the planned per-install random admin password lands (FR-017 rework), the password-injection
step is removed and the install-guide PDF stops being confidential.

## Consequences

- **+** Reproducible PDFs from the reviewed Markdown; one command per release.
- **+** CommonMark parsing fixes the list/code/blockquote rendering class of bugs by construction.
- **+** Password handling is explicit and reviewable instead of manual copy-paste.
- **−** A second language (Python) in the repo, for tooling only. Mitigated by keeping it to one
  ~120-line script with no framework and a pinned two-package environment.
- **−** Requires Python 3 and a Chromium-based browser on the developer's machine (Chrome on the
  maintainer's Mac; Edge ships with Windows 10/11).
- **−** Output depends on the local browser version's print engine; minor layout drift between
  machines is acceptable for a handout.

## Alternatives considered

| Option | Why not |
| --- | --- |
| **Pandoc + LaTeX** | Large install (TeX distribution), Vietnamese needs explicit font setup; table styling harder than CSS. |
| **.NET console tool with Markdig + PuppeteerSharp/Playwright** | Keeps one language, but PuppeteerSharp/Playwright download a Chromium build at first run (internet, ~150 MB) and pull more packages than the Python route; a Markdig-only tool would still need an external browser for PDF. Reasonable if Python becomes a burden — the Markdown and CSS carry over unchanged. |
| **Generate PDFs in `release.yml`** | Would publish the credential-bearing PDF as a release artifact, or require two variants; the confidential edition must stay off GitHub. |
| **Keep the ad-hoc script outside the repo** | Not reproducible, not reviewed; it already shipped two rendering bugs. |
| **Ship only Markdown** | Operators on offline PCs read it in Notepad; the password still needs a separate channel. |

## References

- Constitution II (`.specify/memory/constitution.md`) — dependency lock.
- #124 — docs shipped inside the release zip; #125 — initial password moved out of shipped docs.
- markdown-it-py — <https://github.com/executablebooks/markdown-it-py> (MIT).
