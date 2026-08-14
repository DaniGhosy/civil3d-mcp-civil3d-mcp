# plan-vision

Módulo F del catálogo de lectura de planos ("planos no-CAD: PDF o imagen escaneada") — el único
subsistema del proyecto que no toca el plugin C# ni requiere un dibujo vivo en Civil3D. Es un
subproceso Python independiente, invocado por el servidor MCP (`src/tools/domains/planVisionDomain.ts`
vía `src/utils/PythonBridge.ts`) exactamente como el servidor TS invoca al plugin C# — mismo patrón
de "capa fina que despacha a un proceso separado", distinto proceso.

## Por qué Python

Visión por computadora clásica (OpenCV: contornos, template matching) más OCR (Tesseract vía
pytesseract) — el mismo enfoque técnico usado en proyectos previos de detección por
contorno/área/aspect-ratio. No hay equivalente maduro de estas librerías en el ecosistema Node.

## Prerequisitos de sistema (no alcanza con `pip install`)

1. **Python 3.10+** en el PATH.
2. **Tesseract OCR** instalado aparte y en el PATH (o configurar `pytesseract.pytesseract.tesseract_cmd`
   vía la variable de entorno `TESSERACT_CMD`). En Windows, instalador de
   [UB-Mannheim/tesseract](https://github.com/UB-Mannheim/tesseract/wiki). Verificar con
   `tesseract --version`.
3. **PyMuPDF no necesita nada extra** — a diferencia de `pdf2image` (que requiere el binario Poppler
   instalado y en el PATH aparte), PyMuPDF rasteriza PDFs con un solo `pip install`. Por eso se eligió
   PyMuPDF para `rasterize_pdf_page` en vez de `pdf2image`.

## Setup

```bash
cd plan-vision
python -m venv .venv
.venv\Scripts\activate      # Windows
pip install -r requirements.txt
tesseract --version         # confirma que Tesseract está instalado y en el PATH
```

El servidor Node resuelve el intérprete vía la variable de entorno `PLAN_VISION_PYTHON` (default:
`python`). Si usás el venv de arriba, apuntá esa variable a
`plan-vision/.venv/Scripts/python.exe`.

## Contrato del CLI

`cli.py` recibe UN comando como primer argumento y el JSON de parámetros por stdin; imprime un único
JSON por stdout en caso de éxito (exit code 0) o un mensaje de error por stderr con exit code ≠ 0.

```bash
echo '{"pdfPath": "plano.pdf", "page": 0, "dpi": 300}' | python cli.py rasterize_pdf_page
```

Comandos disponibles: `rasterize_pdf_page`, `extract_legend_templates`, `train_symbol_template`,
`detect_symbols_cv`, `ocr_extract_labels`. (`calibrate_scale_from_dimension` es matemática pura y
vive en TS, no acá — ver `civil3d_plan_vision` en el servidor MCP.)

## `detect_symbols_cv`: salvaguardas contra falsos positivos

`TM_CCOEFF_NORMED` (la métrica de correlación usada) divide por la varianza local — sobre una zona
de plano completamente plana, o un template sin ningún borde/contraste interno (un recorte "sólido"
mal hecho), esa división degenera y devuelve ~1.0 en cualquier posición sin que haya símbolo real
ahí. `detect_symbols_cv` se defiende de esto en dos niveles: cada candidato se valida comparando su
proporción de píxeles "tinta" contra la del template (un match real debe verse tan "cargado" como lo
que supuestamente encontró), y un template sin contraste interno se salta de entrada, reportado en
`skippedTemplates` con el motivo, en vez de producir resultados basura en silencio. Recortá los
símbolos con su borde visible (no un relleno sólido) para evitar esto.

## Nivel de confianza, no exactitud

A diferencia de `civil3d_blocks` (bloques reales del dibujo, 100% exacto), todo lo que devuelve este
módulo es una detección probabilística sobre píxeles — depende de la calidad del escaneo y del umbral
de confianza usado. Está pensado para maximizar precisión dentro de visión clásica (multi-escala,
multi-rotación, non-maximum suppression), no para prometer exactitud.
