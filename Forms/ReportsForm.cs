using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using PPEAccounting.Data;
using PPEAccounting.Reports;
using PPEAccounting.Services;
using PPEAccounting.Ui;

namespace PPEAccounting.Forms
{
    /// <summary>
    /// Печатные отчёты. Слева — список форм, справа — параметры выбранного отчёта.
    /// Кнопка открывает предпросмотр, из которого документ уходит на принтер или в PDF.
    /// </summary>
    public sealed class ReportsForm : Form
    {
        private enum ReportKind
        {
            PersonalCard,
            IssueSheet,
            Stock,
            Compliance,
            Journal
        }

        private readonly AppServices _services;

        private readonly ListBox _list = new ListBox();
        private readonly Label _title = new Label();
        private readonly Label _description = new Label();
        private readonly ComboBox _employee = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly ComboBox _department = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly DateTimePicker _from = new DateTimePicker
        {
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "dd.MM.yyyy",
            Width = 130
        };
        private readonly DateTimePicker _to = new DateTimePicker
        {
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "dd.MM.yyyy",
            Width = 130
        };

        private readonly TableLayoutPanel _parameters = new TableLayoutPanel();
        private readonly Panel _employeeRow = new Panel();
        private readonly Panel _periodRow = new Panel();
        private readonly Panel _departmentRow = new Panel();

        private ReportKind _kind = ReportKind.PersonalCard;

        public ReportsForm(AppServices services)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));

            BackColor = Color.White;
            Font = UiTheme.BaseFont;
            AutoScaleMode = AutoScaleMode.Dpi;
            DoubleBuffered = true;

            _list.Dock = DockStyle.Fill;
            _list.Font = new Font("Segoe UI", 11f);
            _list.IntegralHeight = false;
            _list.Items.AddRange(new object[]
            {
                "Личная карточка учёта СИЗ",
                "Ведомость выдачи за период",
                "Ведомость остатков склада",
                "Отчёт о соблюдении норм",
                "Журнал выдачи и возврата"
            });
            _list.SelectedIndex = 0;
            _list.SelectedIndexChanged += (sender, args) => SelectReport();

            var left = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 20, 0) };
            left.Controls.Add(_list);
            left.Controls.Add(new Label
            {
                Text = "Виды отчётов",
                Dock = DockStyle.Top,
                Height = 34,
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                ForeColor = UiTheme.DarkGrey
            });

            var right = BuildRightSide();

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            layout.Controls.Add(left, 0, 0);
            layout.Controls.Add(right, 1, 0);

            Controls.Add(layout);

            BuildParameters();

            _from.Value = new DateTime(DateTime.Today.Year, 1, 1);
            _to.Value = DateTime.Today;

            FillLookups();
            SelectReport();
        }

        private Control BuildRightSide()
        {
            var right = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };

            _title.Dock = DockStyle.Top;
            _title.Height = 46;
            _title.Font = new Font("Segoe UI", 16f, FontStyle.Bold);
            _title.ForeColor = UiTheme.DarkGrey;

            _description.Dock = DockStyle.Top;
            _description.Height = 70;
            _description.Font = new Font("Segoe UI", 10f);
            _description.ForeColor = Color.DimGray;

            _parameters.Dock = DockStyle.Top;
            _parameters.AutoSize = true;
            _parameters.ColumnCount = 1;
            _parameters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 70,
                Padding = new Padding(0, 20, 0, 0),
                WrapContents = false
            };

            var preview = UiTheme.PrimaryButton("Просмотр и печать", 230, 42);
            preview.Click += (sender, args) => ShowPreview();

            var hint = new Label
            {
                Text = "Печать в PDF: в предпросмотре нажмите «Печать»\nи выберите принтер «Microsoft Print to PDF».",
                AutoSize = true,
                ForeColor = Color.Gray,
                Font = new Font("Segoe UI", 8.5f),
                Padding = new Padding(10, 12, 0, 0)
            };

            buttons.Controls.Add(preview);
            buttons.Controls.Add(hint);

            right.Controls.Add(buttons);
            right.Controls.Add(_parameters);
            right.Controls.Add(_description);
            right.Controls.Add(_title);

            return right;
        }

        private void BuildParameters()
        {
            _employeeRow.Height = 42;
            _employeeRow.Dock = DockStyle.Top;
            _employeeRow.Controls.Add(_employee);
            _employeeRow.Controls.Add(new Label { Text = "Сотрудник:", Width = 130, Dock = DockStyle.Left, TextAlign = ContentAlignment.MiddleLeft });
            _employee.Dock = DockStyle.Fill;

            _periodRow.Height = 42;
            _periodRow.Dock = DockStyle.Top;

            var fromLabel = new Label { Text = "Период с:", Width = 90, Dock = DockStyle.Left, TextAlign = ContentAlignment.MiddleLeft };
            _from.Dock = DockStyle.Left;
            var dash = new Label { Text = "по:", Width = 40, Dock = DockStyle.Left, TextAlign = ContentAlignment.MiddleCenter };
            _to.Dock = DockStyle.Left;

            _periodRow.Controls.Add(_to);
            _periodRow.Controls.Add(dash);
            _periodRow.Controls.Add(_from);
            _periodRow.Controls.Add(fromLabel);

            _departmentRow.Height = 42;
            _departmentRow.Dock = DockStyle.Top;
            _departmentRow.Controls.Add(_department);
            _departmentRow.Controls.Add(new Label { Text = "Подразделение:", Width = 130, Dock = DockStyle.Left, TextAlign = ContentAlignment.MiddleLeft });
            _department.Dock = DockStyle.Fill;

            _parameters.Controls.Add(_employeeRow, 0, 0);
            _parameters.Controls.Add(_periodRow, 0, 1);
            _parameters.Controls.Add(_departmentRow, 0, 2);
        }

        private void FillLookups()
        {
            var employees = _services.Employees.GetAll()
                .Select(row => new LookupItem(row.Id, row.FullName + " — " + row.PositionName))
                .ToList();

            UiFactory.FillCombo(_employee, employees, null);

            var departments = new List<LookupItem> { new LookupItem(0, "Все подразделения") };
            departments.AddRange(_services.Reports.GetDepartments());

            UiFactory.FillCombo(_department, departments, 0);
        }

        private void SelectReport()
        {
            _kind = (ReportKind)Math.Max(0, _list.SelectedIndex);

            _employeeRow.Visible = _kind == ReportKind.PersonalCard;
            _periodRow.Visible = _kind == ReportKind.IssueSheet || _kind == ReportKind.Journal;
            _departmentRow.Visible = _kind == ReportKind.IssueSheet || _kind == ReportKind.Compliance ||
                                     _kind == ReportKind.Journal;

            switch (_kind)
            {
                case ReportKind.PersonalCard:
                    _title.Text = "Личная карточка учёта СИЗ";
                    _description.Text =
                        "Форма личной карточки по Инструкции № 209 Минтруда и соцзащиты Республики Беларусь: " +
                        "что выдано работнику, по какой норме и до какой даты действует срок носки. " +
                        "Подписывается работником и специалистом.";
                    break;

                case ReportKind.IssueSheet:
                    _title.Text = "Ведомость выдачи за период";
                    _description.Text =
                        "Все выдачи за выбранный период с подписью получателя. " +
                        "Можно построить по одному подразделению или по всему предприятию.";
                    break;

                case ReportKind.Stock:
                    _title.Text = "Ведомость остатков склада";
                    _description.Text =
                        "Остатки по номенклатуре с минимальным запасом и отклонением. " +
                        "Отрицательное отклонение показывает позиции, которые пора закупать.";
                    break;

                case ReportKind.Compliance:
                    _title.Text = "Отчёт о соблюдении норм";
                    _description.Text =
                        "Положено по норме, выдано за 12 месяцев, остаток лимита и просроченный срок носки. " +
                        "Помогает перед проверкой инспекции труда.";
                    break;

                default:
                    _title.Text = "Журнал выдачи и возврата";
                    _description.Text =
                        "Полный журнал за период: кто получил, кто оформил, подтверждено ли получение " +
                        "и возвращены ли СИЗ на склад.";
                    break;
            }
        }

        private void ShowPreview()
        {
            try
            {
                BuildDocument().ShowPreview(this);
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, "Отчёт",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private ReportDocument BuildDocument()
        {
            var department = _department.SelectedLookup();
            int? departmentId = department == null || department.Id == 0 ? (int?)null : department.Id;

            switch (_kind)
            {
                case ReportKind.PersonalCard:
                    var employee = _employee.SelectedLookup();

                    if (employee == null)
                        throw new InvalidOperationException("Выберите сотрудника.");

                    return ReportFactory.PersonalCard(_services, employee.Id);

                case ReportKind.IssueSheet:
                    return ReportFactory.IssueSheet(_services, _from.Value.Date, _to.Value.Date, departmentId);

                case ReportKind.Stock:
                    return ReportFactory.StockSheet(_services);

                case ReportKind.Compliance:
                    return ReportFactory.Compliance(_services, departmentId);

                default:
                    return ReportFactory.Journal(_services, _from.Value.Date, _to.Value.Date, departmentId);
            }
        }
    }
}
