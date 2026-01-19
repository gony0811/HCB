using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

namespace HCB.UI
{
    public partial class SensorIoItemViewModel : ObservableObject
    {

        private readonly PmacIoDevice _device;

        private readonly string _ioName = string.Empty;

        [ObservableProperty]
        private string name;

        [ObservableProperty]
        private bool isChecked;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ToggleCommand))]
        private bool isReadOnly;

        public SensorIoItemViewModel() { }

        public SensorIoItemViewModel(string ioName, PmacIoDevice pmacIo, string label = "", bool isChecked = false, bool isReadOnly = false)
        {
            try
            {
                _ioName = ioName;
                Name = label;
                IsChecked = isChecked;
                IsReadOnly = isReadOnly;
                _device = pmacIo;

                var io = _device.FindIoDataByName(_ioName);

                if (io is not null && io.IoType == Data.Entity.Type.IoType.DigitalInput)
                {
                    var data = (DigitalInput)io;

                    data.ValueChanged += DI_ValueChanged;
                }
            }
            catch (Exception ex)
            {
                // Log the exception or handle it as needed
                
            }

        }

        private void DI_ValueChanged(object? sender, ValueChangedEventArgs<object> e)
        {
            IsChecked = (bool)e.NewValue;
        }


        [RelayCommand]
        private void Toggle()
        {
            if (IsReadOnly) return;

            _device.SetDigital(_ioName, IsChecked);
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