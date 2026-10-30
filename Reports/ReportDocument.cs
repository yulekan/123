using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace PPEAccounting.Reports
{
    /// <summary>
    /// Печатный документ отчёта: шапка организации, таблицы с рамками, подписи
    /// и нумерация страниц. Один и тот же документ показывается в предпросмотре
    /// и уходит на печать (в том числе в PDF через «Microsoft Print to PDF»).
    /// </summary>
    internal sealed class ReportDocument
    {
        private enum Kind
        {
            Text,
            TableHeader,
            TableRow,
            Signature
        }

        private sealed class Item
        {
            public Kind Kind { get; set; }
            public string Text { get; set; }
            public string[] Cells { get; set; }
            public float[] Widths { get; set; }
            public Font Font { get; set; }
            public float SpaceBefore { get; set; }
            public bool Bold { get; set; }
            public bool Centered { get; set; }
        }

        private sealed class Page
        {
            public List<Item> Items { get; } = new List<Item>();
        }

        private static readonly Font TitleFont = new Font("Arial", 13f, FontStyle.Bold);
        private static readonly Font SubtitleFont = new Font("Arial", 8f, FontStyle.Italic);
        private static readonly Font BodyFont = new Font("Arial", 9f);
        private static readonly Font BoldFont = new Font("Arial", 9f, FontStyle.Bold);
        private static readonly Font TableFont = new Font("Arial", 8f);
        private static readonly Font TableHeaderFont = new Font("Arial", 8f, FontStyle.Bold);
        private static readonly Font SmallFont = new Font("Arial", 7.5f);

        private readonly List<Item> _items = new List<Item>();
        private List<Page> _pages;
        private int _pageIndex;

        public ReportDocument(string title, string subtitle = null)
        {
            Title = title ?? string.Empty;
            Subtitle = subtitle;
        }

        public string Title { get; }

        public string Subtitle { get; }

        public string Organization { get; set; } = "ООО «Велес-Мит»";

        public void AddTitle(string text)
        {
            _items.Add(new Item { Kind = Kind.Text, Text = text, Font = TitleFont, Centered = true, SpaceBefore = 0 });
        }

        public void AddSubtitle(string text)
        {
            _items.Add(new Item { Kind = Kind.Text, Text = text, Font = SubtitleFont, Centered = true, SpaceBefore = 2 });
        }

        public void AddInfo(string label, string value)
        {
            _items.Add(new Item
            {
                Kind = Kind.Text,
                Text = label + ": " + value,
                Font = BodyFont,
                SpaceBefore = 4
            });
        }

        public void AddParagraph(string text, bool bold = false)
        {
            _items.Add(new Item
            {
                Kind = Kind.Text,
                Text = text,
                Font = bold ? BoldFont : BodyFont,
                SpaceBefore = 8
            });
        }

        public void AddNote(string text)
        {
            _items.Add(new Item { Kind = Kind.Text, Text = text, Font = SmallFont, SpaceBefore = 6 });
        }

        public void AddTable(IReadOnlyList<string> headers, float[] widths, IReadOnlyList<string[]> rows)
        {
            _items.Add(new Item
            {
                Kind = Kind.TableHeader,
                Cells = headers.ToArray(),
                Widths = widths,
                Font = TableHeaderFont,
                Bold = true,
                SpaceBefore = 10
            });

            foreach (var row in rows)
            {
                _items.Add(new Item
                {
                    Kind = Kind.TableRow,
                    Cells = row,
                    Widths = widths,
                    Font = TableFont,
                    SpaceBefore = 0
                });
            }

            if (rows.Count == 0)
            {
                _items.Add(new Item
                {
                    Kind = Kind.Text,
                    Text = "Записей нет.",
                    Font = SmallFont,
                    SpaceBefore = 4
                });
            }
        }

        public void AddSignature(string left, string right)
        {
            _items.Add(new Item
            {
                Kind = Kind.Signature,
                Cells = new[] { left, right },
                Font = BodyFont,
                SpaceBefore = 26
            });
        }

        /// <summary>Текстовое представление документа — используется в самопроверке.</summary>
        public string RenderText()
        {
            var builder = new StringBuilder();
            builder.AppendLine(Title);

            if (!string.IsNullOrEmpty(Subtitle))
                builder.AppendLine(Subtitle);

            foreach (var item in _items)
            {
                switch (item.Kind)
                {
                    case Kind.Text:
                        builder.AppendLine(item.Text);
                        break;
                    case Kind.TableHeader:
                    case Kind.TableRow:
                        builder.AppendLine(string.Join(" | ", item.Cells));
                        break;
                    case Kind.Signature:
                        builder.AppendLine(string.Join("        ", item.Cells));
                        break;
                }
            }

            return builder.ToString();
        }

        public int RowCount
        {
            get { return _items.Count(item => item.Kind == Kind.TableRow); }
        }

        public void ShowPreview(IWin32Window owner)
        {
            using (var document = BuildPrintDocument())
            using (var dialog = new PrintPreviewDialog
            {
                Document = document,
                Width = 1100,
                Height = 800,
                WindowState = FormWindowState.Maximized,
                Text = "Предпросмотр: " + Title
            })
            {
                dialog.ShowDialog(owner);
            }
        }

        public PrintDocument BuildPrintDocument()
        {
            var document = new PrintDocument
            {
                DocumentName = Title,
                DefaultPageSettings =
                {
                    Landscape = false,
                    PaperSize = new PaperSize("A4", 827, 1169),
                    Margins = new Margins(70, 40, 50, 50)
                }
            };

            document.PrintPage += OnPrintPage;
            return document;
        }

        /// <summary>
        /// Первая страница в виде картинки: проверка вёрстки и иллюстрации
        /// в документации — без установленного принтера.
        /// </summary>
        public Bitmap RenderFirstPage()
        {
            var document = BuildPrintDocument();
            var settings = document.DefaultPageSettings;

            var bitmap = new Bitmap(settings.PaperSize.Width, settings.PaperSize.Height);
            bitmap.SetResolution(100f, 100f);

            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.White);

                var bounds = MarginBounds(settings);
                EnsurePages(graphics, bounds.Width, bounds.Height);
                DrawPage(graphics, bounds, 0, _pages.Count);

                _pages = null;
                _pageIndex = 0;
            }

            return bitmap;
        }

        private static Rectangle MarginBounds(PageSettings settings)
        {
            var margins = settings.Margins;

            return new Rectangle(
                margins.Left,
                margins.Top,
                settings.PaperSize.Width - margins.Left - margins.Right,
                settings.PaperSize.Height - margins.Top - margins.Bottom);
        }

        private void OnPrintPage(object sender, PrintPageEventArgs e)
        {
            var bounds = e.MarginBounds;

            EnsurePages(e.Graphics, bounds.Width, bounds.Height);

            if (_pageIndex >= _pages.Count)
            {
                e.HasMorePages = false;
                return;
            }

            DrawPage(e.Graphics, bounds, _pageIndex, _pages.Count);

            _pageIndex++;
            e.HasMorePages = _pageIndex < _pages.Count;

            if (!e.HasMorePages)
            {
                // Сброс: документ можно напечатать повторно из предпросмотра.
                _pages = null;
                _pageIndex = 0;
            }
        }

        private void EnsurePages(Graphics graphics, float width, float height)
        {
            if (_pages != null)
                return;

            _pages = Paginate(graphics, width, height);
            _pageIndex = 0;
        }

        private void DrawPage(Graphics graphics, Rectangle bounds, int index, int total)
        {
            var y = (float)bounds.Top;

            // Шапка страницы: организация и название отчёта.
            using (var headerFont = new Font("Arial", 7.5f))
            using (var format = new StringFormat { Alignment = StringAlignment.Near })
            {
                graphics.DrawString(Organization, headerFont, Brushes.Gray, bounds.Left, y, format);
                format.Alignment = StringAlignment.Far;
                graphics.DrawString(Title, headerFont, Brushes.Gray, bounds.Right, y, format);
            }

            y += 18;
            using (var pen = new Pen(Color.Gray, 0.6f))
                graphics.DrawLine(pen, bounds.Left, y, bounds.Right, y);

            y += 8;

            foreach (var item in _pages[index].Items)
                y = DrawItem(graphics, item, bounds.Left, y, bounds.Width);

            // Нижний колонтитул: страница из всего.
            var footer = "Страница " + (index + 1) + " из " + total +
                         "   ·   " + DateTime.Now.ToString("dd.MM.yyyy HH:mm");
            graphics.DrawString(footer, SmallFont, Brushes.Gray, bounds.Left, bounds.Bottom + 8);
        }

        private List<Page> Paginate(Graphics graphics, float width, float height)
        {
            var pages = new List<Page>();
            var page = new Page();
            var used = 0f;
            var available = height - 40;

            foreach (var item in _items)
            {
                var itemHeight = Measure(graphics, item, width) + item.SpaceBefore;

                if (used + itemHeight > available && page.Items.Count > 0)
                {
                    pages.Add(page);
                    page = new Page();
                    used = 0f;

                    // Заголовок таблицы повторяется на каждой новой странице.
                    if (item.Kind == Kind.TableRow)
                    {
                        var header = FindHeader(item);
                        if (header != null)
                        {
                            page.Items.Add(header);
                            used += Measure(graphics, header, width) + header.SpaceBefore;
                        }
                    }
                }

                page.Items.Add(item);
                used += itemHeight;
            }

            if (page.Items.Count > 0)
                pages.Add(page);

            return pages;
        }

        private Item FindHeader(Item row)
        {
            var index = _items.IndexOf(row);

            for (var i = index - 1; i >= 0; i--)
            {
                if (_items[i].Kind == Kind.TableHeader)
                    return _items[i];

                if (_items[i].Kind != Kind.TableRow)
                    break;
            }

            return null;
        }

        private static float Measure(Graphics graphics, Item item, float width)
        {
            switch (item.Kind)
            {
                case Kind.Text:
                    return graphics.MeasureString(item.Text ?? string.Empty, item.Font, (int)Math.Max(width, 100f)).Height;

                case Kind.TableHeader:
                case Kind.TableRow:
                    return MeasureTableRow(graphics, item, width);

                case Kind.Signature:
                    return item.Font.GetHeight(graphics) + 18f;
            }

            return 0f;
        }

        private float DrawItem(Graphics graphics, Item item, float left, float y, float width)
        {
            y += item.SpaceBefore;

            switch (item.Kind)
            {
                case Kind.Text:
                    return DrawText(graphics, item, left, y, width);

                case Kind.TableHeader:
                    return DrawTableRow(graphics, item, left, y, width, true);

                case Kind.TableRow:
                    return DrawTableRow(graphics, item, left, y, width, false);

                case Kind.Signature:
                    return DrawSignature(graphics, item, left, y, width);
            }

            return y;
        }

        private static float DrawText(Graphics graphics, Item item, float left, float y, float width)
        {
            var text = item.Text ?? string.Empty;
            var size = graphics.MeasureString(text, item.Font, (int)width);

            using (var format = new StringFormat
            {
                Alignment = item.Centered ? StringAlignment.Center : StringAlignment.Near
            })
            {
                var rectangle = new RectangleF(left, y, width, size.Height + 2);
                graphics.DrawString(text, item.Font, Brushes.Black, rectangle, format);
            }

            return y + size.Height;
        }

        private static float MeasureTableRow(Graphics graphics, Item item, float width)
        {
            var height = item.Font.GetHeight(graphics) + 6f;

            // Высота строки определяется самой «высокой» ячейкой: длинные
            // наименования и ФИО переносятся на несколько строк.
            for (var i = 0; i < item.Cells.Length; i++)
            {
                var columnWidth = width * (i < item.Widths.Length ? item.Widths[i] : 1f / item.Cells.Length);
                var size = graphics.MeasureString(item.Cells[i] ?? string.Empty, item.Font,
                    (int)Math.Max(columnWidth - 6f, 10f));
                height = Math.Max(height, size.Height + 7f);
            }

            return height;
        }

        private static float DrawTableRow(Graphics graphics, Item item, float left, float y, float width, bool header)
        {
            var height = MeasureTableRow(graphics, item, width);
            var x = left;

            using (var pen = new Pen(Color.Black, 0.5f))
            using (var format = new StringFormat
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Near
            })
            {
                for (var i = 0; i < item.Cells.Length; i++)
                {
                    var columnWidth = width * (i < item.Widths.Length ? item.Widths[i] : 1f / item.Cells.Length);
                    var cell = new RectangleF(x, y, columnWidth, height);

                    graphics.DrawRectangle(pen, x, y, columnWidth, height);
                    graphics.DrawString(item.Cells[i] ?? string.Empty, item.Font, Brushes.Black,
                        new RectangleF(x + 3, y + 3, columnWidth - 6, height - 6), format);

                    x += columnWidth;
                }

                if (header)
                {
                    var last = new RectangleF(x - 1, y, 1, height);
                    graphics.FillRectangle(Brushes.Black, last);
                }
            }

            return y + height;
        }

        private static float DrawSignature(Graphics graphics, Item item, float left, float y, float width)
        {
            var columnWidth = width / 2f;

            using (var pen = new Pen(Color.Black, 0.5f))
            {
                graphics.DrawLine(pen, left, y + 18, left + columnWidth - 20, y + 18);
                graphics.DrawLine(pen, left + columnWidth, y + 18, left + width - 20, y + 18);
            }

            graphics.DrawString(item.Cells[0], SmallFont, Brushes.Gray, left, y);
            graphics.DrawString(item.Cells[1], SmallFont, Brushes.Gray, left + columnWidth, y);

            return y + 18 + SmallFont.GetHeight(graphics) + 4;
        }
    }
}
