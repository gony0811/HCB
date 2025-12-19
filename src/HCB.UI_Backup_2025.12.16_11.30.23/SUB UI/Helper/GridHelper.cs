using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;

namespace HCB.UI
{
    public static class GridHelper
    {
        // 📌 Columns attached property
        public static readonly DependencyProperty ColumnsProperty =
            DependencyProperty.RegisterAttached(
                "Columns",
                typeof(string),
                typeof(GridHelper),
                new PropertyMetadata(null, OnColumnsChanged));

        public static void SetColumns(DependencyObject element, string value)
            => element.SetValue(ColumnsProperty, value);

        public static string GetColumns(DependencyObject element)
            => (string)element.GetValue(ColumnsProperty);

        private static void OnColumnsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var grid = d as Grid;
            if (grid == null) return;
            grid.ColumnDefinitions.Clear();

            var converter = new GridLengthConverter();
            var parts = (e.NewValue as string)?.Split(',').Select(p => p.Trim());
            if (parts == null) return;

            foreach (var part in parts)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = (GridLength)converter.ConvertFromString(part)
                });
            }
        }

        // 📌 Rows attached property
        public static readonly DependencyProperty RowsProperty =
            DependencyProperty.RegisterAttached(
                "Rows",
                typeof(string),
                typeof(GridHelper),
                new PropertyMetadata(null, OnRowsChanged));

        public static void SetRows(DependencyObject element, string value)
            => element.SetValue(RowsProperty, value);

        public static string GetRows(DependencyObject element)
            => (string)element.GetValue(RowsProperty);

        private static void OnRowsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var grid = d as Grid;
            if (grid == null) return;
            grid.RowDefinitions.Clear();

            var converter = new GridLengthConverter();
            var parts = (e.NewValue as string)?.Split(',').Select(p => p.Trim());
            if (parts == null) return;

            foreach (var part in parts)
            {
                grid.RowDefinitions.Add(new RowDefinition
                {
                    Height = (GridLength)converter.ConvertFromString(part)
                });
            }
        }
    }
}
