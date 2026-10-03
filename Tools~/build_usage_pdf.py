"""Rebuild the two USAGE PDFs from Markdown. Requires PyMuPDF.

Handles the guides' headings, paragraphs, lists, quotes, code and pipe tables.
Unity ignores Tools~. Run from any directory: python Tools~/build_usage_pdf.py
"""
from pathlib import Path
import html
import re
import tempfile
import pymupdf as fitz

ROOT = Path(__file__).resolve().parents[1]


def inline(text):
    text = html.escape(text.replace('\\|', '|'))
    text = re.sub(r'`([^`]+)`', r'<code>\1</code>', text)
    text = re.sub(r'\*\*([^*]+)\*\*', r'<b>\1</b>', text)
    text = re.sub(r'(?<!\*)\*([^*]+)\*(?!\*)', r'<i>\1</i>', text)
    return text


def markdown(source):
    lines = source.splitlines()
    result = []
    i = 0
    while i < len(lines):
        line = lines[i].strip()
        i += 1
        if not line or line == '---':
            continue
        if line.startswith('```'):
            code = []
            while i < len(lines) and not lines[i].strip().startswith('```'):
                code.append(lines[i])
                i += 1
            i += 1
            result.append('<pre>' + html.escape('\n'.join(code)) + '</pre>')
        elif line.startswith('|'):
            rows = [line]
            while i < len(lines) and lines[i].strip().startswith('|'):
                rows.append(lines[i].strip())
                i += 1
            out = ['<table>']
            n = 0
            for row in rows:
                cells = re.split(r'(?<!\\)\|', row[1:-1])
                if all(re.fullmatch(r'\s*:?-+:?\s*', c) for c in cells):
                    continue
                tag = 'th' if n == 0 else 'td'
                out.append('<tr>' + ''.join(f'<{tag}>{inline(c.strip())}</{tag}>' for c in cells) + '</tr>')
                n += 1
            result.append(''.join(out) + '</table>')
        elif line.startswith('#'):
            level = len(line) - len(line.lstrip('#'))
            result.append(f'<h{level}>' + inline(line[level:].strip()) + f'</h{level}>')
        elif line.startswith('>'):
            result.append('<blockquote>' + inline(line.lstrip('> ')) + '</blockquote>')
        elif re.match(r'(?:[-*]|\d+\.) ', line):
            result.append('<p class="list">' + inline(line) + '</p>')
        else:
            result.append('<p>' + inline(line) + '</p>')
    return '\n'.join(result)


CSS = """body { font-family: sans-serif; font-size: 9pt; line-height: 1.5; color: #242424; }
h1 { font-size: 21pt; margin: 0 0 16pt; }
h2 { font-size: 15pt; margin: 20pt 0 9pt; border-bottom: 1px solid #777; }
h3 { font-size: 12pt; margin: 15pt 0 7pt; }
h4 { font-size: 10pt; margin: 12pt 0 6pt; }
h1,h2,h3,h4 { page-break-after: avoid; }
p { margin: 0 0 6pt; }
.list { margin-left: 10pt; }
blockquote { margin: 6pt 0 9pt; padding: 6pt 9pt; background-color: #f2f2f2; }
code { font-family: sans-serif; color: #444; }
pre { font-family: sans-serif; font-size: 8pt; padding: 8pt; background-color: #f2f2f2; }
table { border-collapse: collapse; width: 100%; margin: 8pt 0 12pt; }
th,td { border: 0.5px solid #aaa; padding: 5pt; vertical-align: top; font-size: 8pt; }
th { background-color: #ededed; }
"""


def build(name):
    source = ROOT / 'Docs' / (name + '.md')
    story = fitz.Story(html=markdown(source.read_text(encoding='utf-8')), user_css=CSS)
    temp = Path(tempfile.gettempdir()) / (name + '-story.pdf')
    writer = fitz.DocumentWriter(str(temp))
    page = fitz.paper_rect('a4')
    area = fitz.Rect(42, 42, page.width - 42, page.height - 42)
    more = True
    while more:
        device = writer.begin_page(page)
        more, _ = story.place(area)
        story.draw(device)
        writer.end_page()
    writer.close()
    doc = fitz.open(temp)
    for index, pdfpage in enumerate(doc):
        pdfpage.insert_text((42, 23), 'DenEmo | Usage Guide', fontsize=8, color=(.4, .4, .4))
        pdfpage.insert_text((page.width / 2 - 12, page.height - 22), f'{index + 1} / {len(doc)}', fontsize=8)
    doc.set_metadata({'title': 'DenEmo Usage Guide' + (' (Japanese)' if name.endswith('_ja') else ''), 'author': 'dennokoworks'})
    output = source.with_suffix('.pdf')
    doc.save(output, garbage=4, deflate=True)
    print(f'{output.name}: {len(doc)} pages')
    doc.close()


if __name__ == '__main__':
    for name in ('USAGE', 'USAGE_ja'):
        build(name)
