using CommunityToolkit.Mvvm.ComponentModel;
using HCB.Data.Entity.Type;
using HCB.Data.Entity;

namespace HCB.UI
{
    public partial class MotionParameterCreateVM : ObservableObject
    {
        [ObservableProperty] private string name;
        [ObservableProperty] private ValueType valueType = ValueType.String;
        [ObservableProperty] private string value;
        [ObservableProperty] private UnitType unit;

        public MotionParameter ToEntity()
        {

            return new MotionParameter
            {
                Name = this.Name,
                BoolValue = ValueType == ValueType.Boolean ? bool.Parse(Value) : null,
                IntValue = ValueType == ValueType.Integer ? int.Parse(Value) : null,
                DoubleValue = (ValueType == ValueType.Double || ValueType == ValueType.Float) ? double.Parse(Value) : null,
                StringValue = ValueType == ValueType.String ? Value : null,
                ValueType = this.ValueType,
                UnitType = this.Unit
            };
        }

    }
}
