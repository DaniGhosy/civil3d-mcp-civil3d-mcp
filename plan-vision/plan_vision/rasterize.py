"""rasterize_pdf_page — PDF page to image, via PyMuPDF. Chosen over pdf2image specifically to
avoid depending on the Poppler binary being installed and on PATH (real friction on Windows);
PyMuPDF is a single `pip install`.
"""
import pymupdf


def rasterize_pdf_page(pdf_path: str, page: int, output_path: str, dpi: int = 300) -> dict:
    doc = pymupdf.open(pdf_path)
    try:
        if page < 0 or page >= doc.page_count:
            raise ValueError(f"Page {page} out of range — document has {doc.page_count} page(s).")

        pixmap = doc.load_page(page).get_pixmap(dpi=dpi)
        pixmap.save(output_path)

        return {
            "imagePath": output_path,
            "width": pixmap.width,
            "height": pixmap.height,
            "page": page,
            "dpi": dpi,
        }
    finally:
        doc.close()
