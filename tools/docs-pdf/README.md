# docs-pdf — operator docs to PDF (ADR-0008)

Developer-only. Renders `docs/huong-dan-cai-dat.md`, `docs/runbook.md` and
`docs/deployment-offline.md` to A4 PDFs for handing over to gate operators. Not part of the
solution, CI, release or the offline zip.

## Run

Needs Python 3.10+ (markdown-it-py 4.x) and Chrome, Chromium or Microsoft Edge.

```bash
python3 -m venv .venv-docs-pdf
.venv-docs-pdf/bin/pip install -r tools/docs-pdf/requirements.txt
.venv-docs-pdf/bin/python tools/docs-pdf/build_docs_pdf.py
```

On Windows use `.venv-docs-pdf\Scripts\python.exe`. Output: `tools/docs-pdf/out/` (git-ignored).
If the browser is not found, set `B2_PDF_BROWSER` to its executable.

## ⚠️ The install-guide PDF is confidential

`huong-dan-cai-dat.pdf` gets the initial admin password (read from `SeedAdminPassword` in
`tests/B2.Server.Api.Tests/Support/AuthHttpExtensions.cs`) and a red "TÀI LIỆU MẬT" banner.
Never commit it, attach it to a GitHub Release, or send it by email. Hand it over separately from
the USB.

`--no-password` builds a shareable edition that keeps the "see the handed-over PDF" sentence.

The build stops if the password step in `docs/huong-dan-cai-dat.md` § 4 no longer matches
`PASSWORD_PLACEHOLDER` in the script — update both together.
