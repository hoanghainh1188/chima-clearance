"""Export the operator docs to PDF (ADR-0008). Developer-only; not part of the product.

Renders docs/huong-dan-cai-dat.md, docs/runbook.md and docs/deployment-offline.md with
markdown-it-py (CommonMark + tables) and prints them to A4 PDF with a headless
Chromium-based browser (Chrome, Chromium or Edge) already installed on this machine.

No credential is injected: since F-01 (ADR-0009) each install shows its own initial Admin
password on screen, so the PDFs are plain handouts. Output is still git-ignored.

    python tools/docs-pdf/build_docs_pdf.py                  # -> tools/docs-pdf/out/
"""

from __future__ import annotations

import argparse
import html
import os
import re
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

from markdown_it import MarkdownIt

REPO = Path(__file__).resolve().parents[2]
TOOL_DIR = Path(__file__).resolve().parent
DOCS = {
    "huong-dan-cai-dat.md": "Hướng dẫn cài đặt",
    "runbook.md": "Sổ tay vận hành",
    "deployment-offline.md": "Triển khai offline",
}
BROWSER_CANDIDATES = [
    "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome",
    "/Applications/Chromium.app/Contents/MacOS/Chromium",
    "/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge",
    r"C:\Program Files\Google\Chrome\Application\chrome.exe",
    r"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
    r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
    r"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
]
BROWSER_COMMANDS = ["google-chrome", "chromium", "chromium-browser", "microsoft-edge"]


def find_browser() -> str:
    override = os.environ.get("B2_PDF_BROWSER")
    if override:
        return override
    for path in BROWSER_CANDIDATES:
        if Path(path).is_file():
            return path
    for command in BROWSER_COMMANDS:
        found = shutil.which(command)
        if found:
            return found
    sys.exit("No Chrome/Chromium/Edge found. Set B2_PDF_BROWSER to the browser executable.")


def release_label() -> str:
    try:
        tag = subprocess.run(
            ["git", "describe", "--tags", "--abbrev=0"],
            cwd=REPO, capture_output=True, text=True, check=True,
        ).stdout.strip()
        return f"B2 Clearance {tag}"
    except (OSError, subprocess.CalledProcessError):
        return "B2 Clearance"


def rewrite_links(body: str) -> str:
    """Point links between the three docs at the sibling PDFs; neutralise other repo paths."""
    def fix(match: re.Match[str]) -> str:
        target = match.group(1)
        if target.split("#")[0] in DOCS:
            return f'href="{target.split("#")[0].replace(".md", ".pdf")}"'
        return match.group(0) if re.match(r"https?://", target) else 'href="#"'
    return re.sub(r'href="([^"]+)"', fix, body)


def render_html(name: str, source: str, css: str, label: str) -> str:
    source = source.replace("- [ ] ", "- ☐ ").replace("- [x] ", "- ☑ ")
    body = rewrite_links(MarkdownIt("commonmark", {"html": False}).enable("table").render(source))
    return (
        f'<!doctype html><html lang="vi"><head><meta charset="utf-8">'
        f"<title>{html.escape(DOCS[name])}</title><style>{css}</style></head>"
        f'<body>{body}<p class="footer-src">Nguồn: docs/{name} — {html.escape(label)}</p>'
        f"</body></html>"
    )


def print_pdf(browser: str, html_path: Path, pdf_path: Path) -> None:
    # No --user-data-dir: with a fresh profile headless Chrome writes the PDF but never exits.
    pdf_path.unlink(missing_ok=True)
    subprocess.run(
        [
            browser, "--headless=new", "--disable-gpu", "--no-pdf-header-footer",
            f"--print-to-pdf={pdf_path}", html_path.as_uri(),
        ],
        capture_output=True, check=False, timeout=120,
    )
    if not pdf_path.is_file() or pdf_path.stat().st_size == 0:
        sys.exit(f"Browser did not produce {pdf_path}")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--out", type=Path, default=TOOL_DIR / "out", help="output folder")
    args = parser.parse_args()

    browser = find_browser()
    css = (TOOL_DIR / "print.css").read_text(encoding="utf-8")
    label = release_label()
    args.out.mkdir(parents=True, exist_ok=True)

    with tempfile.TemporaryDirectory() as tmp:
        tmp_dir = Path(tmp)
        for name in DOCS:
            source = (REPO / "docs" / name).read_text(encoding="utf-8-sig")
            html_path = tmp_dir / name.replace(".md", ".html")
            html_path.write_text(render_html(name, source, css, label), encoding="utf-8")
            pdf_path = args.out.resolve() / name.replace(".md", ".pdf")
            print_pdf(browser, html_path, pdf_path)
            print(pdf_path)


if __name__ == "__main__":
    main()
