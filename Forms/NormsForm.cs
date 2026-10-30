using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using PPEAccounting.Data;
using PPEAccounting.Ui;

namespace PPEAccounting.Forms
{
    /// <summary>Нормы выдачи СИЗ по должностям.</summary>
    public sealed class NormsForm : Form, IRefreshable
    {
        private readonly INormRepository _norms;
        private readonly ILookupRepository _lookups;
        private readonly DataGridView _grid;

        private IReadOnlyList<LookupItem> _items = new List<LookupItem>();
        private IReadOnlyList<LookupItem> _positions = new List<LookupItem>();

        public NormsForm(INormRepository norms, ILookupRepository lookups)
        {
            _norms = norms ?? throw new ArgumentNullException(nameof(norms));
            _lookups = lookups ?? throw new ArgumentNullException(nameof(lookups));

            BackColor = Color.White;
            Font = UiTheme.BaseFont;
            AutoScaleMode = AutoScaleMode.Dpi;
            DoubleBuffered = true;

            _grid = UiFactory.CreateGrid();

            var toolbar = UiFactory.Toolbar();

            var addButton = UiTheme.PrimaryButton("Добавить норму", 150);
            addButton.Click += (sender, args) => OpenDialog(null);

            var editButton = UiTheme.SecondaryButton("Изменить", 130);
            editButton.Click += (sender, args) => EditSelected();

            var deleteButton = UiTheme.SecondaryButton("Удалить", 130);
            deleteButton.Click += (sender, args) => DeleteSelected();

            toolbar.Controls.Add(addButton);
            toolbar.Controls.Add(editButton);
            toolbar.Controls.Add(deleteButton);

            Controls.Add(_grid);
            Controls.Add(toolbar);
            Controls.Add(UiFactory.SectionTitle("Нормы выдачи СИЗ"));

            Reload();
        }

        public void Reload()
        {
            var selected = SelectedNorm()?.Id;

            _items = _lookups.GetItems();
            _positions = _lookups.GetPositions();
            UiFactory.Bind(_grid, _norms.GetAll(), row => row.Id, selected);
        }

        private NormRow SelectedNorm()
        {
            return _grid.CurrentRow?.DataBoundItem as NormRow;
        }

        private void EditSelected()
        {
            var row = SelectedNorm();
            if (row == null)
            {
                MessageBox.Show(this, "Выберите норму в таблице.", "Нормы",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            OpenDialog(row);
        }

        private void OpenDialog(NormRow row)
        {
            // Должности могли добавить в карточке сотрудника — обновляем список перед открытием нормы.
            _positions = _lookups.GetPositions();

            using (var dialog = new NormDialog(_norms, _lookups, _items, _positions, row))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    Reload();
            }
        }

        private void DeleteSelected()
        {
            var row = SelectedNorm();
            if (row == null)
            {
                MessageBox.Show(this, "Выберите норму в таблице.", "Нормы",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var question = "Удалить норму: " + row.ItemName + " — " + row.PositionName + "?";

            if (MessageBox.Show(this, question, "Подтверждение", MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            try
            {
                _norms.Delete(row.Id);
                Reload();
            }
            catch (InvalidOperationException exception)
            {
                MessageBox.Show(this, exception.Message, "Удаление невозможно",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private sealed class NormDialog : DialogForm
        {
            private readonly INormRepository _repository;
            private readonly ILookupRepository _lookups;
            private readonly int? _id;

            private readonly ComboBox _item = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            private readonly ComboBox _position = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            private readonly ToolTip _tooltip = new ToolTip();
            private readonly NumericUpDown _quantity = new NumericUpDown { Minimum = 1, Maximum = 1000, Value = 1 };
            private readonly NumericUpDown _months = new NumericUpDown { Minimum = 1, Maximum = 120, Value = 12 };

            private IReadOnlyList<LookupItem> _positions;

            public NormDialog(INormRepository repository,
                ILookupRepository lookups,
                IReadOnlyList<LookupItem> items,
                IReadOnlyList<LookupItem> positions,
                NormRow row)
                : base(row == null ? "Новая норма выдачи" : "Изменение нормы", 560, 350, 5, 140)
            {
                _repository = repository;
                _lookups = lookups;
                _positions = positions;
                _id = row?.Id;

                var addPosition = UiTheme.SecondaryButton("+", 38, 30);
                addPosition.Margin = Padding.Empty;
                _tooltip.SetToolTip(addPosition, "Добавить должность");
                addPosition.Click += (sender, args) => AddPosition();

                UiFactory.FillCombo(_item, items, row?.ItemId);
                UiFactory.FillCombo(_position, _positions, row?.PositionId);

                AddField(0, "Вид СИЗ:", _item);
                AddField(1, "Должность:", UiFactory.WithButton(_position, addPosition));
                AddField(2, "Кол-во за 12 мес.:", _quantity);
                AddField(3, "Срок носки, мес.:", _months);

                Body.Controls.Add(new Label
                {
                    Text = "Норма и срок носки — справочные. Лимит выдачи проверяется за скользящие 12 месяцев.",
                    ForeColor = Color.DimGray,
                    AutoSize = false,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft
                }, 1, 4);

                if (row != null)
                {
                    _quantity.Value = row.QtyPerYear;
                    _months.Value = row.WearPeriodMonths;
                }
            }

            /// <summary>Создаёт должность и сразу выбирает её в списке.</summary>
            private void AddPosition()
            {
                var departments = _lookups.GetDepartments();
                if (departments.Count == 0)
                {
                    Warn("Сначала добавьте подразделение.");
                    return;
                }

                using (var dialog = new PositionDialog(_lookups, departments))
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK)
                        return;

                    _positions = _lookups.GetPositions();
                    UiFactory.FillCombo(_position, _positions, dialog.PositionId);
                }
            }

            protected override bool TrySave()
            {
                var item = _item.SelectedLookup();
                if (item == null)
                {
                    Warn("Выберите вид СИЗ.");
                    return false;
                }

                var position = _position.SelectedLookup();
                if (position == null)
                {
                    Warn("Выберите должность.");
                    return false;
                }

                var quantity = (int)_quantity.Value;
                var months = (int)_months.Value;

                if (_id.HasValue)
                    _repository.Update(_id.Value, item.Id, position.Id, quantity, months);
                else
                    _repository.Add(item.Id, position.Id, quantity, months);

                return true;
            }
        }
    }
}
