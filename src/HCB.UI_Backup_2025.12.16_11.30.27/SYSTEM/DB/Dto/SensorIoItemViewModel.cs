using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace HCB.UI
{
    public partial class SensorIoItemViewModel : ObservableObject
    {

        [ObservableProperty]
        private string name;

        [ObservableProperty]
        private bool isChecked;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ToggleCommand))]
        private bool isReadOnly;

        public SensorIoItemViewModel() { }

        public SensorIoItemViewModel(string name, bool isChecked = false, bool isReadOnly = false)
        {
            Name = name;
            IsChecked = isChecked;
            IsReadOnly = isReadOnly;
        }

        [RelayCommand]
        private void Toggle()
        {
            if (IsReadOnly) return;
        }

        private bool CanToggle() => !IsReadOnly;

        // ReadOnly가 바뀌면 CanExecute 새로고침
        partial void OnIsCheckedChanged(bool oldValue, bool newValue)
        {
            if (!IsReadOnly)
            {
                // _ioService.WriteOutput(Name, newValue);
            }
        }
    }

}