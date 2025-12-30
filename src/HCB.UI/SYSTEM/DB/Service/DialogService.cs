using HCB.IoC;
using HCB.UI;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Telerik.Windows.Controls;

[Service(Lifetime.Singleton)]
public class DialogService
{
    public DialogService() { }

    public Task<bool?> ShowEditDialog(object vm)
    {
        var currentOwner = GetOwnerWindow(); 
        var modal = new PromptModal
        {
            DataContext = vm,
            Owner = currentOwner,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        return Task.FromResult(modal.ShowDialog());
    }

    public Task<bool?> ShowDetailEditModal(object vm, string header = "Edit", int width = 400, int height = 800)
    {
        var currentOwner = GetOwnerWindow();
        var modal = new CreateModal
        {
            Header = header,
            DataContext = vm,
            Width = width,
            Height = height,
            Owner = currentOwner,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        return Task.FromResult(modal.ShowDialog());
    }

    public void ShowMessage(string title, string content)
    {
        RadWindow.Alert(new DialogParameters
        {
            Header = title,
            Content = content,
            Owner = GetOwnerWindow(), // 실시간 Owner 주입
        });
    }

    public bool ShowConfirm(string title, string content)
    {
        bool result = false;
        RadWindow.Confirm(new DialogParameters
        {
            Header = title,
            Content = content,
            Owner = GetOwnerWindow(),
            Closed = (s, e) => { result = e.DialogResult == true; }
        });
        return result;
    }

    public double? ShowEditNumDialog(double value, double minValue, double maxValue)
    {
        var currentOwner = GetOwnerWindow();
        var modal = new UNmumPad(value, minValue, maxValue)
        {
            Owner = currentOwner,
            WindowStartupLocation = WindowStartupLocation.CenterOwner 
        };

        if (modal.ShowDialog() == false) return null;
        return modal.ResultValue;
    }

    // static 메서드를 활용해 현재 가장 적절한 윈도우를 찾음
    public static Window GetOwnerWindow()
    {
        // 1. 현재 포커스가 있는 윈도우
        var activeWindow = Application.Current.Windows.OfType<Window>().FirstOrDefault(x => x.IsActive);

        // 2. 없으면 메인 윈도우, 그것도 없으면 리스트의 마지막 윈도우
        return activeWindow
               ?? Application.Current.MainWindow
               ?? Application.Current.Windows.OfType<Window>().LastOrDefault();
    }
}