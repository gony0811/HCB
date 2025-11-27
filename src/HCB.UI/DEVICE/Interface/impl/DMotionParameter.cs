using CommunityToolkit.Mvvm.ComponentModel;
using HCB.Data.Entity;
using HCB.Data.Entity.Type;

namespace HCB.UI
{
    public partial class DMotionParameter : ObservableObject
    {
        [ObservableProperty] private int id;
        [ObservableProperty] private string name;
        [ObservableProperty] private ValueType valueType;
        [ObservableProperty] private string stringValue;
        [ObservableProperty] private int? intValue;
        [ObservableProperty] private double? doubleValue;
        [ObservableProperty] private bool? boolValue;
        [ObservableProperty] private UnitType unit;
        [ObservableProperty] private IMotion parentMotion;
    }
}
