using HCB.IoC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

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

        public bool ShowMessage(string title, string content)
        {
            return AlertModal.Ask(Owner, title, content);
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
