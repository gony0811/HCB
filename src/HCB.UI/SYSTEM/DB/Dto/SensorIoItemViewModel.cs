using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using Serilog.Core;
using System;

namespace HCB.UI
{
    public partial class SensorIoItemViewModel : ObservableObject
    {
        private readonly PmacIoDevice _device;
        private ILogger _logger;
        [ObservableProperty]
        private string ioName;

        [ObservableProperty]
        private string name;

        public SharedIoState SharedState { get; }

        public bool IsChecked
        {
            get => SharedState.IsChecked;
            set => SharedState.IsChecked = value;
        }

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ToggleCommand))]
        private bool isReadOnly;

        [ObservableProperty]
        private string description;

        public SensorIoItemViewModel() { }

        public SensorIoItemViewModel(ILogger logger, string ioName, SharedIoState sharedState, PmacIoDevice pmacIo, string label = "", string description="", bool isReadOnly = false)
        {
            this._logger = logger;
            try
            {
                IoName = ioName;
                Name = label;
                SharedState = sharedState;
                IsReadOnly = isReadOnly;
                _device = pmacIo;
                Description = description;

                //var io = _device.FindIoDataByName(IoName);

                //if (io is not null && io.IoType == Data.Entity.Type.IoType.DigitalInput)
                //{
                //    var data = (DigitalInput)io;

                //    data.ValueChanged += DI_ValueChanged;
                //}
            }
            catch (Exception ex)
            {
                // Log the exception or handle it as needed
                
            }

        }

        //private void DI_ValueChanged(object? sender, ValueChangedEventArgs<object> e)
        //{
        //    IsChecked = (bool)e.NewValue;
        //}


        [RelayCommand]
        public void Toggle()
        {
            if (IsReadOnly) return;
            try
            {
                _device.SetDigital(IoName, IsChecked);
            }catch(Exception e)
            {
                _logger.Error(e.Message);
            }
            
        }

        public void On()
        {
            if (IsReadOnly) return;
            try
            {
                _device.SetDigital(IoName, true);
            }catch(Exception e)
            {
                _logger.Error(e.Message);
            }
            
        } 

        public void Off()
        {
            if (IsReadOnly) return;
            try
            {
                _device.SetDigital(IoName, false);
            }catch(Exception e)
            {
                _logger.Error(e.Message);
            }
            
        }

        private bool CanToggle() => !IsReadOnly;

        // ReadOnly가 바뀌면 CanExecute 새로고침
        //partial void OnIsCheckedChanged(bool oldValue, bool newValue)
        //{
        //    if (!IsReadOnly)
        //    {
        //        // _ioService.WriteOutput(Name, newValue);
        //    }
        //}
    }

}