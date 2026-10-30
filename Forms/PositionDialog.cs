using System;
using System.Collections.Generic;
using System.Windows.Forms;
using PPEAccounting.Data;
using PPEAccounting.Ui;

namespace PPEAccounting.Forms
{
    /// <summary>
    /// Создание новой должности: название и подразделение.
    /// Открывается кнопкой «+» рядом с выбором должности.
    /// </summary>
    internal sealed class PositionDialog : DialogForm
    {
        private readonly ILookupRepository _lookups;
        private readonly TextBox _name = new TextBox();
        private readonly ComboBox _department = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };

        public PositionDialog(ILookupRepository lookups, IReadOnlyList<LookupItem> departments)
            : base("Новая должность", 480, 230, 2, 130)
        {
            _lookups = lookups ?? throw new ArgumentNullException(nameof(lookups));

            AddField(0, "Название:", _name);
            AddField(1, "Подразделение:", _department);

            UiFactory.FillCombo(_department, departments, null);
        }

        /// <summary>Id созданной (или уже существующей) должности.</summary>
        public int PositionId { get; private set; }

        protected override bool TrySave()
        {
            var name = _name.Text.Trim();
            if (name.Length == 0)
            {
                Warn("Введите название должности.");
                return false;
            }

            var department = _department.SelectedLookup();
            if (department == null)
            {
                Warn("Выберите подразделение.");
                return false;
            }

            PositionId = _lookups.GetOrCreatePosition(name, department.Id);
            return true;
        }
    }
}
