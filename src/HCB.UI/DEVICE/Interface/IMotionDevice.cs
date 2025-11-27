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

        ObservableCollection<IMotion> MotionList { get; }
        IMotion FindMotionByMotorIndex(int mIndex);
        IMotion FindMotionByName(string name);
    }
}
