# Regenerates testdata/sample.pdf (12 pages, A4, searchable text, NEEDLE_MARKER on pages 4/8/12).
# Requires: pip install pymupdf
import pymupdf

doc = pymupdf.open()
doc.set_metadata({"title": "Sample Document", "author": "PdfReaderLight tests", "subject": "test fixture"})
for i in range(1, 13):
    page = doc.new_page(width=595, height=842)  # A4 portrait
    page.insert_text((72, 90), f"Sample Document - Page {i}", fontsize=24, fontname="hebo")
    body = (
        "This is a generated test PDF used to verify the lightweight reader. "
        "It contains searchable text on every page so that full-text search can be tested. "
        "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor "
        "incididunt ut labore et dolore magna aliqua. "
    ) * (3 + i)
    page.insert_textbox(pymupdf.Rect(72, 130, 523, 760), body, fontsize=11, fontname="helv")
    if i % 4 == 0:
        page.insert_text((72, 800), "NEEDLE_MARKER search target on this page", fontsize=10, fontname="helv")
doc.save("testdata/sample.pdf")
doc.close()
print("wrote testdata/sample.pdf")
