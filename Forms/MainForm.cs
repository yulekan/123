using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using PPEAccounting.Services;
using PPEAccounting.Ui;

namespace PPEAccounting.Forms
{
    /// <summary>
    /// Главное окно. Разделы доступны без входа в систему: приложение рассчитано
    /// на одного пользователя — специалиста по охране труда. Формы создаются один
    /// раз и переиспользуются, поэтому при переключении не теряются фильтры.
    /// </summary>
    public sealed class MainForm : Form
    {
        private readonly AppServices _services;
        private readonly Panel _content;
        private readonly Label _pageTitle;
        private readonly Dictionary<Type, Form> _forms = new Dictionary<Type, Form>();
        private readonly List<RoundedButton> _menuButtons = new List<RoundedButton>();
        private RoundedButton _homeButton;

        public MainForm(AppServices services)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));

            Text = "ООО «Велес-Мит» — Учёт спецодежды и СИЗ";
            ClientSize = new Size(1280, 800);
            MinimumSize = new Size(1024, 768);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = UiTheme.LightBackground;
            Font = UiTheme.BaseFont;
            AutoScaleMode = AutoScaleMode.Dpi;
            DoubleBuffered = true;

            var header = BuildHeader(out _pageTitle);
            var sidebar = BuildSidebar();

            _content = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(30),
                BackColor = UiTheme.LightBackground
            };

            // Порядок добавления задаёт порядок стыковки: сначала заполняющая панель,
            // затем боковое меню и шапка (они «отрезают» себе края).
            Controls.Add(_content);
            Controls.Add(sidebar);
            Controls.Add(header);

            Navigate(_homeButton, "Главная страница", null, null);
        }

        private Panel BuildHeader(out Label title)
        {
            var header = new Panel { Dock = DockStyle.Top, Height = 70, BackColor = Color.White };

            title = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(30, 0, 0, 0),
                Font = UiTheme.PageTitleFont,
                ForeColor = UiTheme.DarkGrey,
                Text = "Главная страница"
            };

            header.Controls.Add(title);

            header.Paint += (sender, args) =>
            {
                using (var pen = new Pen(Color.FromArgb(220, 220, 220)))
                    args.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
            };

            return header;
        }

        private Panel BuildSidebar()
        {
            var sidebar = new Panel { Dock = DockStyle.Left, Width = 260, BackColor = UiTheme.DarkGrey };

            // FlowLayoutPanel задаёт порядок пунктов сверху вниз явно:
            // с Dock = Top порядок зависел бы от порядка добавления.
            var menu = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(0, 10, 0, 10)
            };

            AddMenuButton(menu, "Главная", null, null);
            AddMenuButton(menu, "Сотрудники", typeof(EmployeesForm),
                () => new EmployeesForm(_services.Employees, _services.Lookups));
            AddMenuButton(menu, "Нормы выдачи", typeof(NormsForm),
                () => new NormsForm(_services.Norms, _services.Lookups));
            AddMenuButton(menu, "Склад СИЗ", typeof(PpeItemsForm),
                () => new PpeItemsForm(_services.Items, _services.Lookups, _services.StockService));
            AddMenuButton(menu, "Журнал выдачи", typeof(IssueForm),
                () => new IssueForm(_services.IssueService, _services.Lookups, _services.Audit));
            AddMenuButton(menu, "Отчёты", typeof(ReportsForm), () => new ReportsForm(_services));
            AddMenuButton(menu, "Журнал действий", typeof(AuditForm), () => new AuditForm(_services));

            var logo = new Label
            {
                Text = "ВЕЛЕС-МИТ\nУчёт СИЗ",
                ForeColor = Color.White,
                Font = UiTheme.LogoFont,
                Dock = DockStyle.Top,
                Height = 120,
                TextAlign = ContentAlignment.MiddleCenter
            };

            sidebar.Controls.Add(menu);
            sidebar.Controls.Add(logo);

            return sidebar;
        }

        private void AddMenuButton(Panel sidebar, string text, Type formType, Func<Form> formFactory)
        {
            var button = new RoundedButton
            {
                Text = text,
                Height = 52,
                Width = 260,
                ForeColor = Color.Gainsboro,
                Font = UiTheme.MenuFont,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(28, 0, 0, 0),
                BackColor = UiTheme.DarkGrey,
                HoverColor = UiTheme.SidebarHover,
                SelectedColor = UiTheme.Burgundy,
                BorderRadius = 0,
                Margin = new Padding(0)
            };

            button.Click += (sender, args) => Navigate(button, text, formType, formFactory);

            sidebar.Controls.Add(button);
            _menuButtons.Add(button);

            if (formType == null)
                _homeButton = button;
        }

        private void Navigate(RoundedButton button, string title, Type formType, Func<Form> formFactory)
        {
            foreach (var menuButton in _menuButtons)
                menuButton.Selected = ReferenceEquals(menuButton, button);

            _pageTitle.Text = title;
            ClearContent();

            if (formType == null)
            {
                _content.Controls.Add(BuildWelcome());
                return;
            }

            Form form;
            if (!_forms.TryGetValue(formType, out form))
            {
                form = formFactory();
                _forms[formType] = form;
            }

            form.TopLevel = false;
            form.FormBorderStyle = FormBorderStyle.None;
            form.Dock = DockStyle.Fill;
            form.BackColor = Color.White;

            _content.Controls.Add(form);
            form.Show();

            var refreshable = form as IRefreshable;
            if (refreshable != null)
                refreshable.Reload();
        }

        private void ClearContent()
        {
            foreach (var control in _content.Controls.Cast<Control>().ToList())
            {
                _content.Controls.Remove(control);

                var cached = control as Form;
                if (cached != null && _forms.ContainsValue(cached))
                    continue;

                control.Dispose();
            }
        }

        private Control BuildWelcome()
        {
            var text =
                "Добро пожаловать в систему учёта спецодежды ООО «Велес-Мит»!\n\n" +
                "• «Сотрудники» — личные карточки работников.\n" +
                "• «Нормы выдачи» — правила выдачи по должностям.\n" +
                "• «Склад СИЗ» — остатки, приход и номенклатура.\n" +
                "• «Журнал выдачи» — оформление выдачи, возврата и подтверждения получения.\n" +
                "• «Отчёты» — личная карточка, ведомости и печать в PDF.\n" +
                "• «Журнал действий» — история выполненных операций.\n\n" +
                "Все данные хранятся локально, в одном файле базы SQLite.";

            return new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                Font = UiTheme.WelcomeFont,
                ForeColor = UiTheme.DarkGrey,
                TextAlign = ContentAlignment.MiddleCenter
            };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var form in _forms.Values)
                    form.Dispose();

                _forms.Clear();
            }

            base.Dispose(disposing);
        }
    }
}
