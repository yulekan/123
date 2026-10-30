# -*- coding: utf-8 -*-
"""Вспомогательные функции оформления docx в стиле шаблона отчёта."""

from docx.shared import Pt, Cm
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.oxml.ns import qn
from docx.oxml import OxmlElement

TNR = 'Times New Roman'
COURIER = 'Courier New'

ALIGN = {
    'justify': WD_ALIGN_PARAGRAPH.JUSTIFY,
    'center': WD_ALIGN_PARAGRAPH.CENTER,
    'left': WD_ALIGN_PARAGRAPH.LEFT,
    'right': WD_ALIGN_PARAGRAPH.RIGHT,
}


def set_pstyle(p, style_id):
    """Назначает абзацу стиль по идентификатору (например, 13 — Заголовок №1)."""
    pPr = p._p.get_or_add_pPr()
    pStyle = pPr.find(qn('w:pStyle'))
    if pStyle is None:
        pStyle = OxmlElement('w:pStyle')
        pPr.insert(0, pStyle)
    pStyle.set(qn('w:val'), style_id)


def fmt(p, align='justify', indent=1.25, spacing=1.5, before=0, after=0, keep=False,
        left_indent=None):
    pf = p.paragraph_format
    pf.alignment = ALIGN[align]
    pf.first_line_indent = Cm(indent) if indent is not None else None
    if left_indent is not None:
        pf.left_indent = Cm(left_indent)
    pf.line_spacing = spacing
    pf.space_before = Pt(before)
    pf.space_after = Pt(after)
    pf.keep_with_next = keep
    return p


def run_fmt(run, size=14, bold=False, italic=False, name=TNR, color=None):
    run.font.name = name
    run.font.size = Pt(size)
    run.bold = bold
    run.italic = italic
    if color is not None:
        run.font.color.rgb = color
    rPr = run._r.get_or_add_rPr()
    rFonts = rPr.find(qn('w:rFonts'))
    if rFonts is None:
        rFonts = OxmlElement('w:rFonts')
        rPr.insert(0, rFonts)
    for attr in ('w:ascii', 'w:hAnsi', 'w:cs', 'w:eastAsia'):
        rFonts.set(qn(attr), name)
    return run


def set_table_style(table, style_id='aa'):
    """Стиль таблицы как в шаблоне (aa — сетка таблицы)."""
    tblPr = table._tbl.tblPr
    el = tblPr.find(qn('w:tblStyle'))
    if el is None:
        el = OxmlElement('w:tblStyle')
        tblPr.insert(0, el)
    el.set(qn('w:val'), style_id)


def shade_cell(cell, fill):
    tcPr = cell._tc.get_or_add_tcPr()
    shd = OxmlElement('w:shd')
    shd.set(qn('w:val'), 'clear')
    shd.set(qn('w:color'), 'auto')
    shd.set(qn('w:fill'), fill)
    tcPr.append(shd)


class Builder:
    """Сборка документа: заголовки, текст, рисунки, таблицы, листинги."""

    def __init__(self, doc):
        self.doc = doc
        self.body = doc.element.body

    # --- служебное -----------------------------------------------------
    def capture(self, callback):
        """Возвращает список XML-элементов, добавленных callback в конец тела.

        Используется элемент-маркер: он вставляется в позицию, куда python-docx
        добавляет новое содержимое (перед завершающим w:sectPr), поэтому все
        созданные в callback элементы оказываются строго перед ним.
        """
        sentinel = OxmlElement('w:p')
        sect = self.body.find(qn('w:sectPr'))
        if sect is not None:
            sect.addprevious(sentinel)
        else:
            self.body.append(sentinel)

        callback()

        # python-docx вставляет новое содержимое непосредственно перед w:sectPr,
        # то есть сразу после маркера.
        block = []
        for element in sentinel.itersiblings():
            if element is sect:
                break
            block.append(element)
        self.body.remove(sentinel)
        return block

    def move_before(self, elements, paragraph):
        for el in elements:
            paragraph._p.addprevious(el)

    def delete(self, paragraph):
        paragraph._p.getparent().remove(paragraph._p)

    def delete_element(self, element):
        element.getparent().remove(element)

    # --- текст ---------------------------------------------------------
    def p(self, text, align='justify', indent=1.25, spacing=1.5, size=14, bold=False,
          italic=False, before=0, after=0, style=None):
        par = self.doc.add_paragraph()
        if style:
            set_pstyle(par, style)
        fmt(par, align=align, indent=indent, spacing=spacing, before=before, after=after)
        if text:
            run_fmt(par.add_run(text), size=size, bold=bold, italic=italic)
        return par

    def rich(self, parts, align='justify', indent=1.25, spacing=1.5, size=14, after=0):
        """parts — список кортежей (текст, жирный, курсив)."""
        par = self.doc.add_paragraph()
        fmt(par, align=align, indent=indent, spacing=spacing, after=after)
        for item in parts:
            text, bold, italic = (item + (False, False))[:3] if isinstance(item, tuple) else (item, False, False)
            run_fmt(par.add_run(text), size=size, bold=bold, italic=italic)
        return par

    def h1(self, text, page_break=True):
        par = self.doc.add_paragraph()
        set_pstyle(par, '13')
        fmt(par, align='center', indent=None, spacing=1.0, before=0, after=12, keep=True)
        par.paragraph_format.page_break_before = page_break
        run_fmt(par.add_run(text.upper()), size=16, bold=True)
        return par

    def h2(self, text):
        par = self.doc.add_paragraph()
        set_pstyle(par, '20')
        fmt(par, align='center', indent=None, spacing=1.0, before=12, after=8, keep=True)
        run_fmt(par.add_run(text), size=14, bold=True)
        return par

    def h3(self, text):
        par = self.doc.add_paragraph()
        fmt(par, align='left', indent=1.25, spacing=1.5, before=8, after=4, keep=True)
        run_fmt(par.add_run(text), size=14, bold=True)
        return par

    def bullet(self, text, size=14):
        par = self.doc.add_paragraph()
        fmt(par, align='justify', indent=None, spacing=1.5, after=0, left_indent=1.25)
        run_fmt(par.add_run('\u2013 ' + text), size=size)
        return par

    def empty(self):
        return self.doc.add_paragraph()

    # --- рисунки и таблицы --------------------------------------------
    def image(self, path, caption, width_cm=15.5):
        par = self.doc.add_paragraph()
        fmt(par, align='center', indent=None, spacing=1.0, before=6, after=2, keep=True)
        par.add_run().add_picture(path, width=Cm(width_cm))
        cap = self.doc.add_paragraph()
        fmt(cap, align='center', indent=None, spacing=1.0, before=0, after=10)
        run_fmt(cap.add_run(caption), size=12)
        return par

    def table(self, rows, caption=None, widths=None, header=True, size=12, spacing=1.0):
        if caption:
            cap = self.doc.add_paragraph()
            fmt(cap, align='center', indent=None, spacing=1.0, before=6, after=4, keep=True)
            run_fmt(cap.add_run(caption), size=12)
        table = self.doc.add_table(rows=len(rows), cols=len(rows[0]))
        set_table_style(table)
        table.autofit = True
        for i, row in enumerate(rows):
            for j, value in enumerate(row):
                cell = table.cell(i, j)
                par = cell.paragraphs[0]
                fmt(par, align='left' if not (header and i == 0) else 'center',
                    indent=None, spacing=spacing, before=0, after=0)
                run_fmt(par.add_run(str(value)), size=size, bold=(header and i == 0))
        if widths:
            for row in table.rows:
                for j, width in enumerate(widths):
                    row.cells[j].width = Cm(width)
        after = self.doc.add_paragraph()
        fmt(after, align='left', indent=None, spacing=1.0, before=0, after=0)
        return table

    def code(self, text, size=9):
        for line in text.splitlines():
            par = self.doc.add_paragraph()
            fmt(par, align='left', indent=None, spacing=1.0, before=0, after=0)
            run_fmt(par.add_run(line if line else ' '), size=size, name=TNR)

    def code_file(self, path, caption, max_lines=400, size=9):
        with open(path, encoding='utf-8-sig') as f:
            lines = f.read().splitlines()
        if len(lines) > max_lines:
            lines = lines[:max_lines] + ['/* ... фрагмент сокращён, полный текст — в файле проекта ... */']
        cap = self.doc.add_paragraph()
        fmt(cap, align='left', indent=None, spacing=1.0, before=8, after=4, keep=True)
        run_fmt(cap.add_run(caption), size=12, bold=True)
        self.code('\n'.join(lines), size=size)
