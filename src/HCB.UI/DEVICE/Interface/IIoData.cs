using HCB.Data.Entity.Type;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HCB.UI
{
    public interface IIoData
    {
        int Id { get; set; }
        string Name { get; set; }

        int Index { get; set; }
        string Address { get; set; }
        IoType IoType { get; set; }
        UnitType Unit { get; set; }
        bool IsEnabled { get; set; }

        string Description { get; set; }
    }
}
