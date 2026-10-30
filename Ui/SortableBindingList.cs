using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace PPEAccounting.Ui
{
    /// <summary>
    /// BindingList с поддержкой сортировки: без него DataGridView не сортирует строки
    /// по клику на заголовок (прежняя версия на DataTable это умела).
    /// </summary>
    internal sealed class SortableBindingList<T> : BindingList<T>
    {
        private bool _sorted;
        private ListSortDirection _direction;
        private PropertyDescriptor _property;

        public SortableBindingList(List<T> items)
            : base(items)
        {
        }

        protected override bool SupportsSortingCore
        {
            get { return true; }
        }

        protected override bool IsSortedCore
        {
            get { return _sorted; }
        }

        protected override PropertyDescriptor SortPropertyCore
        {
            get { return _property; }
        }

        protected override ListSortDirection SortDirectionCore
        {
            get { return _direction; }
        }

        protected override void ApplySortCore(PropertyDescriptor property, ListSortDirection direction)
        {
            var items = Items as List<T>;
            if (items == null)
                return;

            var comparer = Comparer<object>.Default;

            items.Sort((left, right) =>
            {
                var leftValue = property.GetValue(left);
                var rightValue = property.GetValue(right);

                if (leftValue == null)
                    return rightValue == null ? 0 : -1;

                if (rightValue == null)
                    return 1;

                return comparer.Compare(leftValue, rightValue);
            });

            if (direction == ListSortDirection.Descending)
                items.Reverse();

            _sorted = true;
            _property = property;
            _direction = direction;

            OnListChanged(new ListChangedEventArgs(ListChangedType.Reset, -1));
        }

        protected override void RemoveSortCore()
        {
            _sorted = false;
            _property = null;
        }
    }
}
