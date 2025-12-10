using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace HCB.UI
{
    public partial class USub07ViewModel : ObservableObject 
    {
        [ObservableProperty]
        private ObservableCollection<SensorIoItemViewModel> inputIoItems = new ObservableCollection<SensorIoItemViewModel>();

        [ObservableProperty]
        private ObservableCollection<SensorIoItemViewModel> outputIoItems = new ObservableCollection<SensorIoItemViewModel>();

        public USub07ViewModel()
        {
            // Sensor Item 정리
            // 입력 신호 샘플  
            InputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: true));
            InputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: true));
            InputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: true));
            InputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: true));
            InputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: true));
            InputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: true));
            InputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: true));
            InputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: true));
            InputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: true));
            InputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: true));
            InputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: true));
            InputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: true));
            InputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: true));
            InputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: true));
            InputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: true));
            InputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: true));
            InputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: true));
            InputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: true));
            InputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: true));
            InputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: true));
            InputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: true));
            InputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: true));
            InputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: true));
            InputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: true));
            InputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: true));
            InputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: true));
            InputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: true));
            InputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: true));
            InputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: true));
            InputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: true));
            InputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: true));
            InputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: true));
            InputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: true));


            OutputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: false));
            OutputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: false));
            OutputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: false));
            OutputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: false));
            OutputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: false));
            OutputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: false));
            OutputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: false));
            OutputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: false));
            OutputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: false));
            OutputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: false));
            OutputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: false));
            OutputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: false));
            OutputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: false));
            OutputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: false));
            OutputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: false));
            OutputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: false));
            OutputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: false));
            OutputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: false));
            OutputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: false));
            OutputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: false));
            OutputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: false));
            OutputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: false));
            OutputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: false));
            OutputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: false));
            OutputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: false));
            OutputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: false));
            OutputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: false));
            OutputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: false));
            OutputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: false));
            OutputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: false));
            OutputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: false));
            OutputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: false));
            OutputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: false));
            OutputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: false));
            OutputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: false));
            OutputIoItems.Add(new SensorIoItemViewModel("센서 G", isChecked: true, isReadOnly: false));
        }
    }
}
