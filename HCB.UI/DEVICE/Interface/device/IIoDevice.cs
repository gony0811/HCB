using HCB.Data.Entity.Type;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HCB.UI
{
    public interface IIoDevice : IDevice
    {
        public string Ip { get; set; }
        public int Port { get; set; }

        public IoDeviceType IoDeviceType { get; set; }

        public ObservableCollection<IIoData> IoDataList { get; set; }
    }
}
