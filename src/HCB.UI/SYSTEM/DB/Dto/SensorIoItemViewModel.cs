using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using System;

namespace HCB.UI
{
    public partial class SensorIoItemViewModel : ObservableObject
    {
        private readonly PmacIoDevice _device;
        private ILogger _logger;

        [ObservableProperty] private string ioName;
        [ObservableProperty] private string name;
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ToggleCommand))]
        private bool isReadOnly;
        [ObservableProperty] private string description;

        public SharedIoState SharedState { get; }
        public bool IsChecked
        {
            get => SharedState.IsChecked;
            set => SharedState.IsChecked = value;
        }

        public SensorIoItemViewModel() { }

        public SensorIoItemViewModel(ILogger logger, string ioName, SharedIoState sharedState,
            PmacIoDevice pmacIo, string label = "", string description = "", bool isReadOnly = false)
        {
            _logger = logger;
            try
            {
                IoName = ioName;
                Name = label;
                SharedState = sharedState;
                IsReadOnly = isReadOnly;
                _device = pmacIo;
                Description = description;

                var io = _device.FindIoDataByName(IoName);
                if (io is DigitalInput di)
                    di.ValueChanged += ValueChanged;
                else if (io is DigitalOutput dout)
                    dout.ValueChanged += ValueChanged;
                else if (io is AnalogInput ai)
                    ai.ValueChanged += ValueChanged;
                else if (io is AnalogOutput ao)
                    ao.ValueChanged += ValueChanged;

                SharedState.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(SharedIoState.IsChecked))
                        OnPropertyChanged(nameof(IsChecked));
                };
            }
            catch (Exception ex)
            {
                _logger?.Error(ex, "SensorIoItemViewModel init failed: {IoName}", ioName);
            }
        }

        private void ValueChanged(object? sender, ValueChangedEventArgs<object> e)
        {
            SharedState.IsChecked = (bool)e.NewValue;
        }

        [RelayCommand(CanExecute = nameof(CanToggle))]
        public void Toggle()
        {
            if (IsReadOnly) return;
            try
            {
                _device.SetDigital(IoName, IsChecked);
            }
            catch (Exception e)
            {
                _logger.Error(e, "Toggle failed: {IoName}", IoName);
            }
        }

        public void On()
        {
            if (IsReadOnly) return;
            try { _device.SetDigital(IoName, true); }
            catch (Exception e) { _logger.Error(e, "On failed: {IoName}", IoName); }
        }

        public void Off()
        {
            if (IsReadOnly) return;
            try { _device.SetDigital(IoName, false); }
            catch (Exception e) { _logger.Error(e, "Off failed: {IoName}", IoName); }
        }

        private bool CanToggle() => !IsReadOnly;
    }
}