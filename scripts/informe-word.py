"""Genera el informe .docx (OOXML) a partir de scripts/informe-contenido.json.

No depende de Word ni de librerias externas: escribe el paquete Open XML a mano.
Uso: python scripts/informe-word.py scripts/informe-contenido.json Informe.docx
"""

import json
import sys
import zipfile
from datetime import datetime, timezone
from xml.sax.saxutils import escape

W_NS = "http://schemas.openxmlformats.org/wordprocessingml/2006/main"
R_NS = "http://schemas.openxmlformats.org/officeDocument/2006/relationships"

TEXT_WIDTH = 9000  # twips utiles entre margenes


# ---------------------------------------------------------------- utilidades
def esc(text):
    return escape(str(text if text is not None else ""))


def run(text, bold=False, size=None, color=None, italic=False, font=None):
    props = []
    if font:
        props.append(
            '<w:rFonts w:ascii="%s" w:hAnsi="%s" w:cs="%s"/>' % (font, font, font)
        )
    if bold:
        props.append("<w:b/>")
    if italic:
        props.append("<w:i/>")
    if color:
        props.append('<w:color w:val="%s"/>' % color)
    if size:
        props.append('<w:sz w:val="%d"/><w:szCs w:val="%d"/>' % (size, size))

    rpr = "<w:rPr>%s</w:rPr>" % "".join(props) if props else ""
    return '<w:r>%s<w:t xml:space="preserve">%s</w:t></w:r>' % (rpr, esc(text))


def para(text, style="Normal", extra_ppr="", runs=None):
    body = runs if runs is not None else run(text)
    return "<w:p><w:pPr><w:pStyle w:val=\"%s\"/>%s</w:pPr>%s</w:p>" % (
        style,
        extra_ppr,
        body,
    )


def page_break():
    return '<w:p><w:pPr><w:pStyle w:val="Normal"/></w:pPr><w:r><w:br w:type="page"/></w:r></w:p>'


def cell(text, width, header=False, fill=None):
    shd = '<w:shd w:val="clear" w:color="auto" w:fill="%s"/>' % fill if fill else ""
    tcpr = (
        '<w:tcPr><w:tcW w:w="%d" w:type="dxa"/>%s<w:vAlign w:val="center"/></w:tcPr>'
        % (width, shd)
    )
    content = run(text, bold=header, size=17)
    p = (
        '<w:p><w:pPr><w:pStyle w:val="CeldaTabla"/></w:pPr>%s</w:p>' % content
    )
    return "<w:tc>%s%s</w:tc>" % (tcpr, p)


def col_widths(headers, rows):
    n = len(headers)
    weights = []
    for i in range(n):
        values = [len(str(headers[i]))] + [len(str(r[i])) for r in rows]
        avg = sum(values) / float(len(values))
        weights.append(max(9.0, min(70.0, avg * 1.15)))
    total = sum(weights)
    raw = [max(900, int(TEXT_WIDTH * w / total)) for w in weights]
    # reajustar para que sume exactamente TEXT_WIDTH
    raw[-1] += TEXT_WIDTH - sum(raw)
    return raw


def table(headers, rows):
    widths = col_widths(headers, rows)
    grid = "".join('<w:gridCol w:w="%d"/>' % w for w in widths)

    borders = (
        "<w:tblBorders>"
        + "".join(
            '<w:%s w:val="single" w:sz="4" w:space="0" w:color="BFBFBF"/>' % edge
            for edge in ("top", "left", "bottom", "right", "insideH", "insideV")
        )
        + "</w:tblBorders>"
    )

    tblpr = (
        "<w:tblPr>"
        '<w:tblW w:w="%d" w:type="dxa"/>' % TEXT_WIDTH
        + borders
        + '<w:tblLayout w:type="fixed"/>'
        '<w:tblCellMar><w:top w:w="40" w:type="dxa"/><w:left w:w="80" w:type="dxa"/>'
        '<w:bottom w:w="40" w:type="dxa"/><w:right w:w="80" w:type="dxa"/></w:tblCellMar>'
        "<w:tblLook w:val=\"04A0\" w:firstRow=\"1\" w:lastRow=\"0\" w:firstColumn=\"0\""
        " w:lastColumn=\"0\" w:noHBand=\"0\" w:noVBand=\"1\"/>"
        "</w:tblPr>"
    )

    head = (
        "<w:tr><w:trPr><w:tblHeader/></w:trPr>"
        + "".join(cell(headers[i], widths[i], header=True, fill="D9D9D9") for i in range(len(headers)))
        + "</w:tr>"
    )

    body = []
    for row in rows:
        body.append(
            "<w:tr>"
            + "".join(cell(row[i], widths[i]) for i in range(len(headers)))
            + "</w:tr>"
        )

    tbl = "<w:tbl>%s<w:tblGrid>%s</w:tblGrid>%s%s</w:tbl>" % (tblpr, grid, head, "".join(body))
    # Word necesita un parrafo despues de la tabla para no pegar el siguiente bloque
    return tbl + '<w:p><w:pPr><w:pStyle w:val="Normal"/><w:spacing w:after="60"/></w:pPr></w:p>'


# ---------------------------------------------------------------- estilos
STYLES = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:styles xmlns:w="%(w)s">
  <w:docDefaults>
    <w:rPrDefault><w:rPr>
      <w:rFonts w:ascii="Calibri" w:hAnsi="Calibri" w:eastAsia="Calibri" w:cs="Calibri"/>
      <w:sz w:val="22"/><w:szCs w:val="22"/><w:lang w:val="es-ES" w:eastAsia="es-ES"/>
    </w:rPr></w:rPrDefault>
    <w:pPrDefault><w:pPr>
      <w:spacing w:after="120" w:line="276" w:lineRule="auto"/>
      <w:jc w:val="both"/>
    </w:pPr></w:pPrDefault>
  </w:docDefaults>
  <w:style w:type="paragraph" w:default="1" w:styleId="Normal">
    <w:name w:val="Normal"/><w:qFormat/>
  </w:style>
  <w:style w:type="paragraph" w:styleId="Titulo">
    <w:name w:val="Title"/><w:basedOn w:val="Normal"/><w:next w:val="Normal"/><w:qFormat/>
    <w:pPr><w:spacing w:before="2600" w:after="160"/><w:jc w:val="left"/></w:pPr>
    <w:rPr><w:b/><w:color w:val="14375E"/><w:sz w:val="56"/><w:szCs w:val="56"/></w:rPr>
  </w:style>
  <w:style w:type="paragraph" w:styleId="Subtitulo">
    <w:name w:val="Subtitle"/><w:basedOn w:val="Normal"/><w:next w:val="Normal"/><w:qFormat/>
    <w:pPr><w:spacing w:after="400"/><w:jc w:val="left"/></w:pPr>
    <w:rPr><w:color w:val="2E5C8A"/><w:sz w:val="30"/><w:szCs w:val="30"/></w:rPr>
  </w:style>
  <w:style w:type="paragraph" w:styleId="Meta">
    <w:name w:val="Meta"/><w:basedOn w:val="Normal"/><w:qFormat/>
    <w:pPr><w:spacing w:after="60"/><w:jc w:val="left"/></w:pPr>
    <w:rPr><w:color w:val="404040"/><w:sz w:val="21"/><w:szCs w:val="21"/></w:rPr>
  </w:style>
  <w:style w:type="paragraph" w:styleId="Heading1">
    <w:name w:val="heading 1"/><w:basedOn w:val="Normal"/><w:next w:val="Normal"/><w:qFormat/>
    <w:pPr><w:keepNext/><w:keepLines/><w:spacing w:before="360" w:after="160"/>
      <w:jc w:val="left"/><w:outlineLvl w:val="0"/></w:pPr>
    <w:rPr><w:b/><w:color w:val="14375E"/><w:sz w:val="32"/><w:szCs w:val="32"/></w:rPr>
  </w:style>
  <w:style w:type="paragraph" w:styleId="Heading2">
    <w:name w:val="heading 2"/><w:basedOn w:val="Normal"/><w:next w:val="Normal"/><w:qFormat/>
    <w:pPr><w:keepNext/><w:spacing w:before="280" w:after="100"/>
      <w:jc w:val="left"/><w:outlineLvl w:val="1"/></w:pPr>
    <w:rPr><w:b/><w:color w:val="2E5C8A"/><w:sz w:val="26"/><w:szCs w:val="26"/></w:rPr>
  </w:style>
  <w:style w:type="paragraph" w:styleId="Heading3">
    <w:name w:val="heading 3"/><w:basedOn w:val="Normal"/><w:next w:val="Normal"/><w:qFormat/>
    <w:pPr><w:keepNext/><w:spacing w:before="220" w:after="80"/>
      <w:jc w:val="left"/><w:outlineLvl w:val="2"/></w:pPr>
    <w:rPr><w:b/><w:color w:val="404040"/><w:sz w:val="23"/><w:szCs w:val="23"/></w:rPr>
  </w:style>
  <w:style w:type="paragraph" w:styleId="Codigo">
    <w:name w:val="Code"/><w:basedOn w:val="Normal"/><w:qFormat/>
    <w:pPr><w:keepLines/><w:shd w:val="clear" w:color="auto" w:fill="F2F2F2"/>
      <w:spacing w:before="0" w:after="0" w:line="240" w:lineRule="auto"/>
      <w:ind w:left="227" w:right="113"/><w:jc w:val="left"/></w:pPr>
    <w:rPr><w:rFonts w:ascii="Consolas" w:hAnsi="Consolas" w:cs="Consolas"/>
      <w:color w:val="1F1F1F"/><w:sz w:val="18"/><w:szCs w:val="18"/></w:rPr>
  </w:style>
  <w:style w:type="paragraph" w:styleId="Vineta">
    <w:name w:val="Bullet"/><w:basedOn w:val="Normal"/><w:qFormat/>
    <w:pPr><w:spacing w:after="60"/><w:ind w:left="510" w:hanging="227"/><w:jc w:val="left"/></w:pPr>
  </w:style>
  <w:style w:type="paragraph" w:styleId="Indice">
    <w:name w:val="TocEntry"/><w:basedOn w:val="Normal"/><w:qFormat/>
    <w:pPr><w:spacing w:after="50"/><w:ind w:left="227"/><w:jc w:val="left"/></w:pPr>
    <w:rPr><w:sz w:val="22"/><w:szCs w:val="22"/></w:rPr>
  </w:style>
  <w:style w:type="paragraph" w:styleId="CeldaTabla">
    <w:name w:val="TableText"/><w:basedOn w:val="Normal"/><w:qFormat/>
    <w:pPr><w:spacing w:before="20" w:after="20" w:line="240" w:lineRule="auto"/>
      <w:jc w:val="left"/></w:pPr>
    <w:rPr><w:sz w:val="17"/><w:szCs w:val="17"/></w:rPr>
  </w:style>
  <w:style w:type="paragraph" w:styleId="PiePagina">
    <w:name w:val="Footer"/><w:basedOn w:val="Normal"/><w:qFormat/>
    <w:pPr><w:spacing w:after="0"/><w:jc w:val="center"/></w:pPr>
    <w:rPr><w:color w:val="808080"/><w:sz w:val="16"/><w:szCs w:val="16"/></w:rPr>
  </w:style>
</w:styles>
""" % {"w": W_NS}


# ---------------------------------------------------------------- documento
def build(data, footer_text):
    blocks = []

    # portada
    blocks.append(para(data["title"], style="Titulo"))
    blocks.append(para(data["subtitle"], style="Subtitulo"))
    for line in data.get("meta", []):
        blocks.append(para(line, style="Meta"))
    blocks.append(page_break())

    # contenido (lista de secciones)
    blocks.append(para("Contenido", style="Heading1"))
    for b in data["blocks"]:
        if b["t"] == "h1":
            blocks.append(para(b["text"], style="Indice"))
    blocks.append(page_break())

    # cuerpo
    for b in data["blocks"]:
        kind = b["t"]
        if kind == "h1":
            blocks.append(para(b["text"], style="Heading1"))
        elif kind == "h2":
            blocks.append(para(b["text"], style="Heading2"))
        elif kind == "h3":
            blocks.append(para(b["text"], style="Heading3"))
        elif kind == "p":
            blocks.append(para(b["text"]))
        elif kind == "bullets":
            for item in b["items"]:
                blocks.append(para("\u2022  " + item, style="Vineta"))
        elif kind == "code":
            for line in b["lines"]:
                text = line if line else " "
                blocks.append(para(text, style="Codigo"))
            blocks.append(
                '<w:p><w:pPr><w:pStyle w:val="Normal"/><w:spacing w:after="80"/></w:pPr></w:p>'
            )
        elif kind == "table":
            blocks.append(table(b["headers"], b["rows"]))
        elif kind == "pagebreak":
            blocks.append(page_break())

    sect = (
        "<w:sectPr>"
        '<w:footerReference w:type="default" r:id="rId1"/>'
        '<w:pgSz w:w="11906" w:h="16838"/>'
        '<w:pgMar w:top="1247" w:right="1361" w:bottom="1247" w:left="1361"'
        ' w:header="708" w:footer="708" w:gutter="0"/>'
        '<w:pgNumType w:start="1"/>'
        "</w:sectPr>"
    )

    document = (
        '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>\n'
        '<w:document xmlns:w="%s" xmlns:r="%s"><w:body>%s%s</w:body></w:document>'
        % (W_NS, R_NS, "".join(blocks), sect)
    )

    footer = (
        '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>\n'
        '<w:ftr xmlns:w="%s" xmlns:r="%s">'
        '<w:p><w:pPr><w:pStyle w:val="PiePagina"/></w:pPr>'
        "%s"
        '<w:fldSimple w:instr=" PAGE "><w:r><w:rPr><w:color w:val="808080"/>'
        '<w:sz w:val="16"/></w:rPr><w:t>1</w:t></w:r></w:fldSimple></w:p></w:ftr>'
        % (W_NS, R_NS, run(footer_text + "  |  página ", size=16, color="808080"))
    )

    return document, footer


def main():
    if len(sys.argv) < 3:
        print("uso: python informe-word.py contenido.json salida.docx")
        return 2

    with open(sys.argv[1], "r", encoding="utf-8") as fh:
        data = json.load(fh)

    out = sys.argv[2]
    document, footer = build(data, "Football Stats DB  |  MongoDB threesixtybets_db")
    now = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")

    content_types = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
<Default Extension="xml" ContentType="application/xml"/>
<Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
<Override PartName="/word/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml"/>
<Override PartName="/word/footer1.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.footer+xml"/>
<Override PartName="/docProps/core.xml" ContentType="application/vnd.openxmlformats-package.core-properties+xml"/>
<Override PartName="/docProps/app.xml" ContentType="application/vnd.openxmlformats-officedocument.extended-properties+xml"/>
</Types>"""

    rels = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
<Relationship Id="rId2" Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties" Target="docProps/core.xml"/>
<Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties" Target="docProps/app.xml"/>
</Relationships>"""

    doc_rels = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/footer" Target="footer1.xml"/>
</Relationships>"""

    core = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<cp:coreProperties xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties"
 xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:dcterms="http://purl.org/dc/terms/"
 xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
<dc:title>%s</dc:title>
<dc:subject>%s</dc:subject>
<dc:creator>Football Stats DB</dc:creator>
<cp:lastModifiedBy>Football Stats DB</cp:lastModifiedBy>
<dcterms:created xsi:type="dcterms:W3CDTF">%s</dcterms:created>
<dcterms:modified xsi:type="dcterms:W3CDTF">%s</dcterms:modified>
</cp:coreProperties>""" % (esc(data["title"]), esc(data["subtitle"]), now, now)

    app = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Properties xmlns="http://schemas.openxmlformats.org/officeDocument/2006/extended-properties">
<Application>Informe Football Stats DB</Application>
</Properties>"""

    with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as zf:
        zf.writestr("[Content_Types].xml", content_types)
        zf.writestr("_rels/.rels", rels)
        zf.writestr("word/document.xml", document)
        zf.writestr("word/_rels/document.xml.rels", doc_rels)
        zf.writestr("word/styles.xml", STYLES)
        zf.writestr("word/footer1.xml", footer)
        zf.writestr("docProps/core.xml", core)
        zf.writestr("docProps/app.xml", app)

    tables = sum(1 for b in data["blocks"] if b["t"] == "table")
    codes = sum(len(b["lines"]) for b in data["blocks"] if b["t"] == "code")
    print("OK %s | tablas=%d | lineas de codigo=%d" % (out, tables, codes))
    return 0


if __name__ == "__main__":
    sys.exit(main())
