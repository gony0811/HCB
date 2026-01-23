using CommunityToolkit.Mvvm.ComponentModel;
using HCB.Data.Entity;
using HCB.Data.Entity.Type;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ValueType = HCB.Data.Entity.Type.ValueType;

namespace HCB.UI
{
    public partial class ParameterCreateDto : ObservableObject
    {
        [ObservableProperty] private string name = "";
        [ObservableProperty] private string value;
        [ObservableProperty] private string maximum;
        [ObservableProperty] private string minimum;
        [ObservableProperty] private ValueType valueType;
        [ObservableProperty] public UnitType unitType;
        [ObservableProperty] private string description;

    }
}
