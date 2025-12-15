using CommunityToolkit.Mvvm.ComponentModel;
using HCB.Data.Entity;
using HCB.Data.Entity.Type;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HCB.UI
{
    public partial class IoDataCreateVM : ObservableObject
    {
        [ObservableProperty] private string name;
        [ObservableProperty] private string address;
        [ObservableProperty] private int index;
        [ObservableProperty] private UnitType unit;
        [ObservableProperty] private IoType ioType;
        [ObservableProperty] private string description;


        public IoDataEntity ToEntity()
        {
            return new IoDataEntity
            {
                Name = this.Name,
                Address = this.Address,
                Index = this.Index,
                Unit = this.Unit,
                IoDataType = this.IoType,
                Description = this.Description,
            };
        }
    }
}
