using System;
using System.Windows;
using System.Windows.Controls;

namespace HCB.UI
{
    public class ValueTemplateSelector : DataTemplateSelector
    {
        public DataTemplate BoolTemplate { get; set; }
        public DataTemplate TextTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is bool)
                return BoolTemplate;

            return TextTemplate;
        }
    }
}
