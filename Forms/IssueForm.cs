using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using PPEAccounting.Data;
using PPEAccounting.Services;
using PPEAccounting.Ui;

namespace PPEAccounting.Forms
{
    /// <summary>Журнал выдачи и возврата СИЗ.</summary>
    public sealed class IssueForm : Form, IRefreshable
    {
        private readonly IIssueService _issueService;
        private readonly ILookupRepository _lookups;
        private readonly AuditService _audit;

        private readonly ComboBox _employee = CreateCombo();
        private readonly ComboBox _item = CreateCombo();
        private readonly DateTimePicker _date = new DateTimePicker
        {
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "dd.MM.yyyy",
            Width = 120
        };
        private readonly NumericUpDown _quantity = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 10000,
            Width = 80
        };
        private readonly Label _info = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = UiTheme.InfoFont,
            ForeColor = Color.DimGray
        };
        private readonly DataGridView _grid;

        private bool _loading;

        public IssueForm(IIssueService issueService, ILookupRepository lookups, AuditService audit)
        {
            _issueService = issueService ?? throw new ArgumentNullException(nameof(issueService));
            _lookups = lookups ?? throw new ArgumentNullException(nameof(lookups));
            _audit = audit ?? throw new ArgumentNullException(nameof(audit));

            BackColor = Color.White;
            Font = UiTheme.BaseFont;
            AutoScaleMode = AutoScaleMode.Dpi;
            DoubleBuffered = true;

            _grid = UiFactory.CreateGrid();

            Controls.Add(_grid);
            Controls.Add(BuildFilters());
            Controls.Add(UiFactory.SectionTitle("Журнал выдачи и возврата"));

            _employee.SelectedIndexChanged += (sender, args) => UpdateInfo();
            _item.SelectedIndexChanged += (sender, args) => UpdateInfo();
            _date.ValueChanged += (sender, args) => UpdateInfo();

            Reload();
        }

        public void Reload()
        {
            _loading = true;
            try
            {
                var employeeId = _employee.SelectedLookup()?.Id;
                var itemId = _item.SelectedLookup()?.Id;
                var issueId = (_grid.CurrentRow?.DataBoundItem as IssueRow)?.Id;

                UiFactory.FillCombo(_employee, _lookups.GetActiveEmployees(), employeeId);
                UiFactory.FillCombo(_item, _lookups.GetItems(), itemId);
                UiFactory.Bind(_grid, _issueService.GetJournal(), row => row.Id, issueId);

                // Длинные значения получают больше места: иначе ФИО и наименование СИЗ обрезаются.
                UiFactory.Weight(_grid, "EmployeeName", 210);
                UiFactory.Weight(_grid, "PositionName", 95);
                UiFactory.Weight(_grid, "ItemName", 150);
                UiFactory.Weight(_grid, "IssueDate", 90);
                UiFactory.Weight(_grid, "Qty", 55);
                UiFactory.Weight(_grid, "ReturnDate", 80);
                UiFactory.Weight(_grid, "Status", 90);
                UiFactory.Weight(_grid, "ReceivedDate", 90);
            }
            finally
            {
                _loading = false;
            }

            UpdateInfo();
        }

        private Control BuildFilters()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 220,
                BackColor = UiTheme.PanelBackground,
                Padding = new Padding(15)
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 4,
                RowCount = 5
            };

            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            for (var i = 0; i < 5; i++)
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, i < 3 ? 34 : (i == 3 ? 40 : 44)));

            layout.Controls.Add(FieldLabel("Сотрудник:"), 0, 0);
            layout.Controls.Add(_employee, 1, 0);
            layout.SetColumnSpan(_employee, 3);

            layout.Controls.Add(FieldLabel("Вид СИЗ:"), 0, 1);
            layout.Controls.Add(_item, 1, 1);
            layout.SetColumnSpan(_item, 3);

            layout.Controls.Add(FieldLabel("Дата выдачи:"), 0, 2);
            layout.Controls.Add(_date, 1, 2);
            layout.Controls.Add(FieldLabel("Кол-во:"), 2, 2);
            layout.Controls.Add(_quantity, 3, 2);

            layout.Controls.Add(_info, 0, 3);
            layout.SetColumnSpan(_info, 4);

            var actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 6, 0, 0),
                WrapContents = false
            };

            var issueButton = UiTheme.PrimaryButton("Выдать", 130, 34);
            issueButton.Click += OnIssueClick;

            var returnButton = UiTheme.SecondaryButton("Вернуть выбранное", 180, 34);
            returnButton.Click += OnReturnClick;

            var confirmButton = UiTheme.SecondaryButton("Подтвердить получение", 210, 34);
            confirmButton.Click += OnConfirmClick;

            actions.Controls.Add(issueButton);
            actions.Controls.Add(returnButton);
            actions.Controls.Add(confirmButton);

            layout.Controls.Add(actions, 0, 4);
            layout.SetColumnSpan(actions, 4);

            panel.Controls.Add(layout);
            return panel;
        }

        private void UpdateInfo()
        {
            if (_loading)
                return;

            var employee = _employee.SelectedLookup();
            var item = _item.SelectedLookup();

            if (employee == null || item == null)
            {
                _info.Text = string.Empty;
                return;
            }

            var availability = _issueService.GetAvailability(employee.Id, item.Id, _date.Value.Date);

            if (!availability.EmployeeFound)
            {
                _info.Text = "Сотрудник не найден.";
                _info.ForeColor = UiTheme.Danger;
                return;
            }

            if (!availability.HasNorm)
            {
                _info.Text = "Должность: " + availability.PositionName +
                             "   ·   норма на этот вид СИЗ не предусмотрена.";
                _info.ForeColor = UiTheme.Danger;
                return;
            }

            var lastIssue = availability.LastIssueDate.HasValue
                ? "   ·   последняя выдача: " +
                  availability.LastIssueDate.Value.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)
                : string.Empty;

            _info.Text = "Должность: " + availability.PositionName +
                         "   ·   норма: " + availability.QtyPerYear + " за 12 мес. (срок носки " +
                         availability.WearPeriodMonths + " мес.)" +
                         "   ·   выдано: " + availability.AlreadyIssued +
                         "   ·   доступно: " + availability.Remaining +
                         lastIssue;

            _info.ForeColor = availability.Remaining > 0 ? UiTheme.Success : UiTheme.Danger;
        }

        private void OnIssueClick(object sender, EventArgs e)
        {
            var employee = _employee.SelectedLookup();
            if (employee == null)
            {
                MessageBox.Show(this, "Выберите сотрудника.", "Выдача", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var item = _item.SelectedLookup();
            if (item == null)
            {
                MessageBox.Show(this, "Выберите вид СИЗ.", "Выдача", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var result = _issueService.Issue(employee.Id, item.Id, (int)_quantity.Value, _date.Value.Date);

            MessageBox.Show(this, result.Message,
                result.Success ? "Выдача оформлена" : "Выдача не оформлена",
                MessageBoxButtons.OK,
                result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

            if (result.Success)
            {
                _audit.Write("Выдача СИЗ", employee.Name + " — " + item.Name + " × " + _quantity.Value);
                Reload();
            }
            else
                UpdateInfo();
        }

        private void OnReturnClick(object sender, EventArgs e)
        {
            var row = _grid.CurrentRow?.DataBoundItem as IssueRow;
            if (row == null)
            {
                MessageBox.Show(this, "Выберите запись в журнале.", "Возврат",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (row.Status == IssueStatus.Returned)
            {
                MessageBox.Show(this, "Эта запись уже возвращена.", "Возврат",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var question = "Оформить возврат на склад?\n\n" + row.ItemName + " — " + row.EmployeeName +
                           ", " + row.Qty + " " + row.IssueDate.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

            if (MessageBox.Show(this, question, "Подтверждение", MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            var result = _issueService.Return(row.Id, DateTime.Today);

            MessageBox.Show(this, result.Message,
                result.Success ? "Возврат оформлен" : "Возврат не оформлен",
                MessageBoxButtons.OK,
                result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

            if (result.Success)
            {
                _audit.Write("Возврат СИЗ", row.EmployeeName + " — " + row.ItemName);
                Reload();
            }
        }

        /// <summary>
        /// Отметка «получено» заполняет дату фактического получения СИЗ. Работник
        /// расписывается в личной карточке, после чего специалист вносит отметку.
        /// </summary>
        private void OnConfirmClick(object sender, EventArgs e)
        {
            var row = _grid.CurrentRow?.DataBoundItem as IssueRow;
            if (row == null)
            {
                MessageBox.Show(this, "Выберите запись в журнале.", "Подтверждение получения",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (row.Status != IssueStatus.Issued || row.ReceivedDate.HasValue)
            {
                MessageBox.Show(this, "По этой записи получение уже подтверждено или СИЗ возвращены.",
                    "Подтверждение получения", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var question = "Отметить, что работник получил СИЗ?\n\n" + row.ItemName + " — " +
                           row.EmployeeName + ", " + row.Qty + " шт., " +
                           row.IssueDate.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

            if (MessageBox.Show(this, question, "Подтверждение получения", MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            var result = _issueService.ConfirmReceipt(row.Id, DateTime.Today);

            MessageBox.Show(this, result.Message,
                result.Success ? "Получение подтверждено" : "Не удалось подтвердить",
                MessageBoxButtons.OK,
                result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

            if (!result.Success)
                return;

            _audit.Write("Подтверждение получения", row.EmployeeName + " — " + row.ItemName);
            Reload();
        }

        private static Label FieldLabel(string text)
        {
            return new Label { Text = text, Anchor = AnchorStyles.Left, AutoSize = true };
        }

        private static ComboBox CreateCombo()
        {
            return new ComboBox
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
        }
    }
}
