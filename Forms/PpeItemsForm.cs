using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using PPEAccounting.Data;
using PPEAccounting.Services;
using PPEAccounting.Ui;

namespace PPEAccounting.Forms
{
    /// <summary>
    /// Склад СИЗ. Остаток меняется только документами: приход, выдача, возврат.
    /// Ручное редактирование остатка убрано — из-за него склад расходился с историей.
    /// </summary>
    public sealed class PpeItemsForm : Form, IRefreshable
    {
        private readonly IPpeItemRepository _items;
        private readonly ILookupRepository _lookups;
        private readonly IStockService _stock;

        private readonly DataGridView _grid;
        private IReadOnlyList<LookupItem> _suppliers = new List<LookupItem>();

        public PpeItemsForm(IPpeItemRepository items, ILookupRepository lookups, IStockService stock)
        {
            _items = items ?? throw new ArgumentNullException(nameof(items));
            _lookups = lookups ?? throw new ArgumentNullException(nameof(lookups));
            _stock = stock ?? throw new ArgumentNullException(nameof(stock));

            BackColor = Color.White;
            Font = UiTheme.BaseFont;
            AutoScaleMode = AutoScaleMode.Dpi;
            DoubleBuffered = true;

            _grid = UiFactory.CreateGrid();

            var toolbar = UiFactory.Toolbar();

            var addButton = UiTheme.PrimaryButton("Добавить");
            addButton.Click += (sender, args) => OpenCard(null);

            var editButton = UiTheme.SecondaryButton("Изменить");
            editButton.Click += (sender, args) => EditSelected();

            var supplyButton = UiTheme.SecondaryButton("Приход");
            supplyButton.Click += (sender, args) => RegisterSupply();

            var deleteButton = UiTheme.SecondaryButton("Удалить");
            deleteButton.Click += (sender, args) => DeleteSelected();

            toolbar.Controls.Add(addButton);
            toolbar.Controls.Add(editButton);
            toolbar.Controls.Add(supplyButton);
            toolbar.Controls.Add(deleteButton);

            Controls.Add(_grid);
            Controls.Add(toolbar);
            Controls.Add(UiFactory.SectionTitle("Склад спецодежды (СИЗ)"));

            Reload();
        }

        public void Reload()
        {
            var selected = SelectedItem()?.Id;

            _suppliers = _lookups.GetSupplierNames();
            UiFactory.Bind(_grid, _items.GetAll(), row => row.Id, selected);
        }

        private PpeItemRow SelectedItem()
        {
            return _grid.CurrentRow?.DataBoundItem as PpeItemRow;
        }

        private void EditSelected()
        {
            var row = SelectedItem();
            if (row == null)
            {
                MessageBox.Show(this, "Выберите вид СИЗ в таблице.", "Склад",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            OpenCard(row);
        }

        private void OpenCard(PpeItemRow row)
        {
            using (var dialog = new ItemDialog(_items, _lookups, _suppliers, row))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    Reload();
            }
        }

        private void RegisterSupply()
        {
            var row = SelectedItem();
            if (row == null)
            {
                MessageBox.Show(this, "Выберите вид СИЗ в таблице.", "Приход",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (var dialog = new SupplyDialog(_stock, _lookups, _suppliers, row))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    Reload();
            }
        }

        private void DeleteSelected()
        {
            var row = SelectedItem();
            if (row == null)
            {
                MessageBox.Show(this, "Выберите вид СИЗ в таблице.", "Склад",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (MessageBox.Show(this, "Удалить «" + row.Name + "» из номенклатуры?", "Подтверждение",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            try
            {
                _items.Delete(row.Id);
                Reload();
            }
            catch (InvalidOperationException exception)
            {
                MessageBox.Show(this, exception.Message, "Удаление невозможно",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private sealed class ItemDialog : DialogForm
        {
            private readonly IPpeItemRepository _repository;
            private readonly ILookupRepository _lookups;
            private readonly int? _id;

            private readonly TextBox _name = new TextBox();
            private readonly TextBox _unit = new TextBox { Text = "шт" };
            private readonly ComboBox _supplier = CreateSupplierCombo();
            private readonly NumericUpDown _stock = new NumericUpDown { Maximum = 1000000 };
            private readonly NumericUpDown _minStock = new NumericUpDown { Maximum = 1000000 };

            public ItemDialog(IPpeItemRepository repository, ILookupRepository lookups,
                IReadOnlyList<LookupItem> suppliers, PpeItemRow row)
                : base(row == null ? "Новый вид СИЗ" : "Карточка СИЗ", 580, 430, 6, 160)
            {
                _repository = repository;
                _lookups = lookups;
                _id = row?.Id;

                FillSupplierCombo(_supplier, suppliers, row?.SupplierName);
                _supplier.Text = row?.SupplierName ?? string.Empty;

                AddField(0, "Наименование:", _name);
                AddField(1, "Ед. изм.:", _unit);
                AddField(2, "Поставщик:", _supplier);

                if (row == null)
                {
                    AddField(3, "Начальный остаток:", _stock);
                }
                else
                {
                    AddField(3, "Остаток:", new Label
                    {
                        Text = row.StockQty + " (меняется приходом и выдачей)",
                        Anchor = AnchorStyles.Left,
                        AutoSize = true,
                        ForeColor = Color.DimGray
                    });

                    _name.Text = row.Name;
                    _unit.Text = row.Unit;
                }

                AddField(4, "Минимум на складе:", _minStock);
                _minStock.Value = row == null ? 0 : row.MinStock;

                Body.Controls.Add(new Label
                {
                    Text = "Минимум используется в отчёте по остаткам: позиции с остатком не выше минимума требуют закупки.",
                    ForeColor = Color.DimGray,
                    AutoSize = false,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft
                }, 1, 5);
            }

            protected override bool TrySave()
            {
                var name = _name.Text.Trim();
                if (name.Length == 0)
                {
                    Warn("Введите наименование СИЗ.");
                    return false;
                }

                var unit = _unit.Text.Trim();
                if (unit.Length == 0)
                    unit = "шт";

                int? supplierId = null;
                var supplierName = _supplier.Text.Trim();
                if (supplierName.Length > 0)
                    supplierId = _lookups.GetOrCreateSupplier(supplierName);

                if (_id.HasValue)
                    _repository.Update(_id.Value, name, unit, (int)_minStock.Value, supplierId);
                else
                    _repository.Add(name, unit, (int)_stock.Value, (int)_minStock.Value, supplierId);

                return true;
            }
        }

        private sealed class SupplyDialog : DialogForm
        {
            private readonly IStockService _stock;
            private readonly ILookupRepository _lookups;
            private readonly int _itemId;

            private readonly DateTimePicker _date = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "dd.MM.yyyy"
            };
            private readonly NumericUpDown _quantity = new NumericUpDown { Minimum = 1, Maximum = 100000 };
            private readonly NumericUpDown _price = new NumericUpDown
            {
                Minimum = 0,
                Maximum = 10000000,
                DecimalPlaces = 2,
                Increment = 10
            };
            private readonly ComboBox _supplier = CreateSupplierCombo();

            public SupplyDialog(IStockService stock, ILookupRepository lookups,
                IReadOnlyList<LookupItem> suppliers, PpeItemRow row)
                : base("Приход на склад", 560, 350, 5, 130)
            {
                _stock = stock;
                _lookups = lookups;
                _itemId = row.Id;

                FillSupplierCombo(_supplier, suppliers, row.SupplierName);
                _supplier.Text = row.SupplierName ?? string.Empty;

                AddField(0, "Вид СИЗ:", new Label
                {
                    Text = row.Name + " (" + row.Unit + ")",
                    Anchor = AnchorStyles.Left,
                    AutoSize = true
                });
                AddField(1, "Дата прихода:", _date);
                AddField(2, "Количество:", _quantity);
                AddField(3, "Цена за ед.:", _price);
                AddField(4, "Поставщик:", _supplier);
            }

            protected override bool TrySave()
            {
                var supplierName = _supplier.Text.Trim();
                if (supplierName.Length == 0)
                {
                    Warn("Укажите поставщика.");
                    return false;
                }

                var supplierId = _lookups.GetOrCreateSupplier(supplierName);
                var result = _stock.RegisterSupply(_itemId, _date.Value.Date, (int)_quantity.Value, _price.Value, supplierId);

                if (!result.Success)
                {
                    Warn(result.Message);
                    return false;
                }

                MessageBox.Show(result.Message + "\nОстаток обновлён.",
                    "Приход", MessageBoxButtons.OK, MessageBoxIcon.Information);

                return true;
            }
        }

        private static ComboBox CreateSupplierCombo()
        {
            return new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDown,
                Anchor = AnchorStyles.Left | AnchorStyles.Right
            };
        }

        private static void FillSupplierCombo(ComboBox combo, IReadOnlyList<LookupItem> suppliers, string selectedName)
        {
            combo.Items.Clear();
            foreach (var supplier in suppliers)
                combo.Items.Add(supplier.Name);

            if (!string.IsNullOrEmpty(selectedName) && combo.Items.Contains(selectedName))
                combo.SelectedItem = selectedName;
        }
    }
}
