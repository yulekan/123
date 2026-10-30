using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using PPEAccounting.Data;
using PPEAccounting.Ui;

namespace PPEAccounting.Forms
{
    /// <summary>Сотрудники: карточки, приём и увольнение (мягкое удаление).</summary>
    public sealed class EmployeesForm : Form, IRefreshable
    {
        private readonly IEmployeeRepository _employees;
        private readonly ILookupRepository _lookups;
        private readonly DataGridView _grid;

        private IReadOnlyList<LookupItem> _positions = new List<LookupItem>();

        public EmployeesForm(IEmployeeRepository employees, ILookupRepository lookups)
        {
            _employees = employees ?? throw new ArgumentNullException(nameof(employees));
            _lookups = lookups ?? throw new ArgumentNullException(nameof(lookups));

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

            var deleteButton = UiTheme.SecondaryButton("Удалить");
            deleteButton.Click += (sender, args) => DeleteSelected();

            toolbar.Controls.Add(addButton);
            toolbar.Controls.Add(editButton);
            toolbar.Controls.Add(deleteButton);

            Controls.Add(_grid);
            Controls.Add(toolbar);
            Controls.Add(UiFactory.SectionTitle("Сотрудники предприятия"));

            Reload();
        }

        public void Reload()
        {
            var selected = SelectedEmployee()?.Id;

            _positions = _lookups.GetPositions();
            UiFactory.Bind(_grid, _employees.GetAll(), row => row.Id, selected);
        }

        private EmployeeRow SelectedEmployee()
        {
            return _grid.CurrentRow?.DataBoundItem as EmployeeRow;
        }

        private void EditSelected()
        {
            var row = SelectedEmployee();
            if (row == null)
            {
                MessageBox.Show(this, "Выберите сотрудника в таблице.", "Сотрудники",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            OpenCard(row);
        }

        private void OpenCard(EmployeeRow row)
        {
            // Должности могли добавить в соседнем разделе — обновляем список перед открытием карточки.
            _positions = _lookups.GetPositions();

            using (var dialog = new EmployeeDialog(_employees, _lookups, _positions, row))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    Reload();
            }
        }

        private void DeleteSelected()
        {
            var row = SelectedEmployee();
            if (row == null)
            {
                MessageBox.Show(this, "Выберите сотрудника в таблице.", "Сотрудники",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (MessageBox.Show(this, "Удалить сотрудника «" + row.FullName + "»?", "Подтверждение",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            try
            {
                _employees.Delete(row.Id);
                Reload();
            }
            catch (InvalidOperationException exception)
            {
                MessageBox.Show(this, exception.Message, "Удаление невозможно",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private sealed class EmployeeDialog : DialogForm
        {
            private readonly IEmployeeRepository _repository;
            private readonly ILookupRepository _lookups;
            private readonly int? _id;
            private readonly TextBox _name = new TextBox();
            private readonly ComboBox _position = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
            private readonly ToolTip _tooltip = new ToolTip();
            private readonly DateTimePicker _hireDate = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "dd.MM.yyyy"
            };
            private readonly CheckBox _active = new CheckBox { Text = "Активен (работает)", AutoSize = true };

            private IReadOnlyList<LookupItem> _positions;

            public EmployeeDialog(IEmployeeRepository repository, ILookupRepository lookups,
                IReadOnlyList<LookupItem> positions, EmployeeRow row)
                : base(row == null ? "Новый сотрудник" : "Карточка сотрудника", 540, 340, 4, 120)
            {
                _repository = repository;
                _lookups = lookups;
                _positions = positions;
                _id = row?.Id;

                var addPosition = UiTheme.SecondaryButton("+", 38, 30);
                addPosition.Margin = Padding.Empty;
                _tooltip.SetToolTip(addPosition, "Добавить должность");
                addPosition.Click += (sender, args) => AddPosition();

                AddField(0, "ФИО:", _name);
                AddField(1, "Должность:", UiFactory.WithButton(_position, addPosition));
                AddField(2, "Дата приёма:", _hireDate);

                _active.Anchor = AnchorStyles.Left;
                Body.Controls.Add(_active, 1, 3);

                UiFactory.FillCombo(_position, _positions, row?.PositionId);

                if (row == null)
                {
                    _hireDate.Value = DateTime.Today;
                }
                else
                {
                    _name.Text = row.FullName;
                    _hireDate.Value = row.HireDate;
                    _active.Checked = row.IsActive;
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
                var fullName = _name.Text.Trim();
                if (fullName.Length == 0)
                {
                    Warn("Введите ФИО сотрудника.");
                    return false;
                }

                var position = _position.SelectedLookup();
                if (position == null)
                {
                    Warn("Выберите должность.");
                    return false;
                }

                if (_id.HasValue)
                    _repository.Update(_id.Value, fullName, position.Id, _active.Checked);
                else
                    _repository.Add(fullName, position.Id, _hireDate.Value.Date);

                return true;
            }
        }
    }
}
