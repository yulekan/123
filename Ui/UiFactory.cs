using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using PPEAccounting.Data;

namespace PPEAccounting.Ui
{
    /// <summary>Общие элементы интерфейса: одинаковые во всех формах таблицы, заголовки и привязка данных.</summary>
    internal static class UiFactory
    {
        public static Label SectionTitle(string text)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Top,
                Height = 45,
                Font = UiTheme.SectionTitleFont,
                ForeColor = UiTheme.DarkGrey
            };
        }

        public static FlowLayoutPanel Toolbar()
        {
            return new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 55,
                Padding = new Padding(0, 10, 0, 0),
                WrapContents = false
            };
        }

        public static DataGridView CreateGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowHeadersVisible = false,
                Font = UiTheme.GridFont,
                ColumnHeadersHeight = 45,
                RowTemplate = { Height = 40 }
            };

            grid.ColumnHeadersDefaultCellStyle.BackColor = UiTheme.DarkGrey;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            grid.ColumnHeadersDefaultCellStyle.Font = UiTheme.ButtonFont;
            grid.EnableHeadersVisualStyles = false;
            grid.AlternatingRowsDefaultCellStyle.BackColor = UiTheme.PanelBackground;
            grid.DefaultCellStyle.SelectionBackColor = UiTheme.Burgundy;
            grid.DefaultCellStyle.SelectionForeColor = Color.White;
            grid.RowsDefaultCellStyle.SelectionBackColor = UiTheme.Burgundy;
            grid.RowsDefaultCellStyle.SelectionForeColor = Color.White;
            grid.AlternatingRowsDefaultCellStyle.SelectionBackColor = UiTheme.Burgundy;
            grid.AlternatingRowsDefaultCellStyle.SelectionForeColor = Color.White;
            grid.GridColor = UiTheme.GridLine;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;

            // Даты и суммы показываем в привычном виде, не завися от текущей культуры.
            grid.CellFormatting += (sender, args) =>
            {
                if (args.Value is DateTime date)
                {
                    args.Value = date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
                    args.FormattingApplied = true;
                }
                else if (args.Value is decimal money)
                {
                    args.Value = money.ToString("N2");
                    args.FormattingApplied = true;
                }
            };

            return grid;
        }

        /// <summary>Привязывает список к таблице и по возможности сохраняет выбранную строку.</summary>
        public static void Bind<T>(DataGridView grid, IReadOnlyList<T> rows, Func<T, int> idSelector, int? selectedId)
        {
            var list = rows == null ? new List<T>() : new List<T>(rows);

            grid.DataSource = null;
            grid.DataSource = new SortableBindingList<T>(list);

            ApplyColumnHeaders<T>(grid);

            if (!selectedId.HasValue || idSelector == null)
                return;

            for (var index = 0; index < list.Count; index++)
            {
                if (idSelector(list[index]) != selectedId.Value)
                    continue;

                var column = grid.Columns.Cast<DataGridViewColumn>().FirstOrDefault(c => c.Visible);
                if (column != null)
                    grid.CurrentCell = grid.Rows[index].Cells[column.Index];

                grid.Rows[index].Selected = true;
                break;
            }
        }

        /// <summary>
        /// Относительная ширина колонки в режиме Fill: 100 — «обычная» колонка.
        /// Нужна там, где встречаются длинные значения (ФИО, наименование СИЗ).
        /// </summary>
        public static void Weight(DataGridView grid, string columnName, float weight)
        {
            if (grid.Columns.Contains(columnName))
                grid.Columns[columnName].FillWeight = weight;
        }

        /// <summary>
        /// DataGridView сам заголовки из DisplayName не берёт, поэтому проставляем их вручную.
        /// </summary>
        private static void ApplyColumnHeaders<T>(DataGridView grid)
        {
            foreach (PropertyDescriptor property in TypeDescriptor.GetProperties(typeof(T)))
            {
                if (grid.Columns.Contains(property.Name))
                    grid.Columns[property.Name].HeaderText = property.DisplayName;
            }
        }

        public static void FillCombo(ComboBox combo, IReadOnlyList<LookupItem> items, int? selectedId)
        {
            var list = items == null ? new List<LookupItem>() : new List<LookupItem>(items);

            combo.DataSource = null;
            combo.DataSource = list;
            combo.DisplayMember = nameof(LookupItem.Name);
            combo.ValueMember = nameof(LookupItem.Id);

            if (selectedId.HasValue)
            {
                var selected = list.FirstOrDefault(item => item.Id == selectedId.Value);
                if (selected != null)
                    combo.SelectedItem = selected;
            }

            if (combo.SelectedIndex < 0 && combo.Items.Count > 0)
                combo.SelectedIndex = 0;
        }

        public static LookupItem SelectedLookup(this ComboBox combo)
        {
            return combo.SelectedItem as LookupItem;
        }

        /// <summary>
        /// Поле ввода с кнопкой справа. Нужно рядом с выпадающим списком, чтобы
        /// добавить значение, не закрывая текущий диалог.
        /// </summary>
        public static Control WithButton(Control editor, Button button, int buttonWidth = 38)
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = Padding.Empty
            };

            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, buttonWidth));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            editor.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            editor.Margin = new Padding(0, 0, 6, 0);

            button.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            button.Margin = Padding.Empty;

            panel.Controls.Add(editor, 0, 0);
            panel.Controls.Add(button, 1, 0);

            return panel;
        }
    }
}
