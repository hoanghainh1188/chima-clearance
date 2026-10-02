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

## No credentials in the PDFs

Since F-01 (ADR-0009) every install shows its own random initial Admin password on screen, so
the PDFs are plain handouts. `out/` stays git-ignored anyway — hand PDFs over with the USB, don't
commit them.
