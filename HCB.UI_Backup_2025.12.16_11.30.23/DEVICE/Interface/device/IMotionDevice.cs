using HCB.Data.Entity;
using HCB.Data.Entity.Type;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Media;

namespace HCB.UI
{
    public interface IMotionDevice : IDevice
    {
        string Ip { get; set; }
        int Port { get; set; }
        MotionDeviceType MotionDeviceType { get; set; }

        ObservableCollection<IAxis> MotionList { get; }
        IAxis FindMotionByMotorIndex(int mIndex);
        IAxis FindMotionByName(string name);
    }
}
