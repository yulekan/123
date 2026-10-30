using System;
using System.Drawing;
using System.Windows.Forms;
using PPEAccounting.Ui;

namespace PPEAccounting.Forms
{
    /// <summary>Журнал действий: только просмотр. Записи создаются автоматически при операциях.</summary>
    public sealed class AuditForm : Form, IRefreshable
    {
        private readonly AppServices _services;
        private readonly DataGridView _grid;

        public AuditForm(AppServices services)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));

            BackColor = Color.White;
            Font = UiTheme.BaseFont;
            AutoScaleMode = AutoScaleMode.Dpi;
            DoubleBuffered = true;

            _grid = UiFactory.CreateGrid();

            var toolbar = UiFactory.Toolbar();

            var refresh = UiTheme.PrimaryButton("Обновить");
            refresh.Click += (sender, args) => Reload();

            toolbar.Controls.Add(refresh);

            Controls.Add(_grid);
            Controls.Add(toolbar);
            Controls.Add(UiFactory.SectionTitle("Журнал действий (последние 500 записей)"));

            Reload();
        }

        public void Reload()
        {
            UiFactory.Bind(_grid, _services.Audit.GetRecent(500), row => row.Id, null);
        }
    }
}
