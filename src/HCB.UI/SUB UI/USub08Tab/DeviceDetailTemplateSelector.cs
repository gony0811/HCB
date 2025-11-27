using System.Windows;
using System.Windows.Controls;

namespace HCB.UI
{
    public class DeviceDetailTemplateSelector : DataTemplateSelector
    {
        public DataTemplate MotionTemplate { get; set; }
        public DataTemplate CameraTemplate { get; set; }
        public DataTemplate IOTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            if (item is MotionDeviceDetailViewModel) return MotionTemplate;

            // if (item is CameraDeviceDetailViewModel)
            //     return CameraTemplate;

            // if (item is IODeviceDetailViewModel)
            //     return IOTemplate;
            return null;
        }
    }
}
