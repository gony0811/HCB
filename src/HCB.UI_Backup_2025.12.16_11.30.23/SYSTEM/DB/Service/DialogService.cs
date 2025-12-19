using HCB.IoC;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Telerik.Windows.Controls;

namespace HCB.UI
{
    [Service(Lifetime.Singleton)]
    public class DialogService
    {
        private Window Owner;
        public DialogService()
        {
            Owner = GetOwnerWindow();
        }

        public Task<bool?> ShowEditDialog(object vm)
        {
            var modal = new PromptModal
            {
                DataContext = vm
            };
            return Task.FromResult(modal.ShowDialog());
        }

        public Task<bool?> ShowDetailEditModal(object vm, string header="Edit", int width=400, int height=800)
        {
            var modal = new CreateModal
            {
                Header = header,
                DataContext = vm,
                Width = width,
                Height = height,
            };
            return Task.FromResult(modal.ShowDialog());
        }

        public void ShowMessage(string title, string content)
        {
            RadWindow.Alert(new DialogParameters
            {
                Header = title,
                Content = content
            });
        }

        public bool ShowConfirm(string title, string content)
        {
            bool result = false;
            RadWindow.Confirm(new DialogParameters
            {
                Header = title,
                Content = content,
                Closed = (s, e) =>
                {
                    result = e.DialogResult == true;
                }
            });
            return result;
        }

        //public double? ShowEditNumDialog(double value, double minValue, double maxValue, double step = 1.0, bool allowDecimal = true)
        //{
        //    var modal = new UNmumPad(value, minValue, maxValue, step, allowDecimal);
        //    return modal.ShowDialogAndGetResult();
        //}

        private static Window GetOwnerWindow()
        {
            var w = Application.Current != null
                ? Application.Current.Windows.OfType<Window>().FirstOrDefault(x => x.IsActive) ?? Application.Current.MainWindow
                : null;
            return w;
        }
    }
}
