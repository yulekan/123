using System;
using System.Drawing;
using System.Windows.Forms;

namespace PPEAccounting.Ui
{
    /// <summary>
    /// Каркас модального диалога: тело из двух колонок и кнопки «Сохранить / Отмена».
    /// Проверка данных выполняется ДО закрытия окна, поэтому введённое не теряется.
    /// </summary>
    internal abstract class DialogForm : Form
    {
        private readonly TableLayoutPanel _body;

        protected DialogForm(string title, int width, int height, int rowCount, int labelWidth = 130)
        {
            Text = title;
            ClientSize = new Size(width, height);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            BackColor = Color.White;
            Font = UiTheme.BaseFont;
            AutoScaleMode = AutoScaleMode.Dpi;

            _body = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20),
                ColumnCount = 2,
                RowCount = Math.Max(rowCount, 1)
            };

            _body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, labelWidth));
            _body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            for (var i = 0; i < rowCount; i++)
                _body.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

            Controls.Add(_body);
            Controls.Add(CreateButtons());
        }

        protected TableLayoutPanel Body
        {
            get { return _body; }
        }

        /// <summary>Сохраняет данные. Возвращает false, если валидация не пройдена.</summary>
        protected abstract bool TrySave();

        protected void AddField(int row, string label, Control editor)
        {
            _body.Controls.Add(new Label
            {
                Text = label,
                Anchor = AnchorStyles.Left,
                AutoSize = true
            }, 0, row);

            editor.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            _body.Controls.Add(editor, 1, row);
        }

        protected static void Warn(string message)
        {
            MessageBox.Show(message, "Проверьте данные", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private Panel CreateButtons()
        {
            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 64,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(20, 15, 20, 0)
            };

            var save = UiTheme.PrimaryButton("Сохранить", 120);
            save.Margin = new Padding(10, 0, 0, 0);
            save.Click += OnSaveClick;

            var cancel = UiTheme.SecondaryButton("Отмена", 100);
            cancel.DialogResult = DialogResult.Cancel;

            panel.Controls.Add(save);
            panel.Controls.Add(cancel);

            AcceptButton = save;
            CancelButton = cancel;

            return panel;
        }

        private void OnSaveClick(object sender, EventArgs e)
        {
            try
            {
                if (TrySave())
                    DialogResult = DialogResult.OK;
            }
            catch (Exception exception)
            {
                MessageBox.Show(this, exception.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
