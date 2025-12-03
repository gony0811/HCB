using CommunityToolkit.Mvvm.ComponentModel;
using HCB.Data.Entity.Type;
using HCB.Data.Entity;

namespace HCB.UI
{
    public partial class MotionParameterCreateVM : ObservableObject
    {
        [ObservableProperty] private string name;
        [ObservableProperty] private ValueType valueType = ValueType.String;
        [ObservableProperty] private object value;
        [ObservableProperty] private UnitType unit;

        public MotionParameter ToEntity()
        {
            return new MotionParameter
            {
                Name = this.Name,
                BoolValue = this.ValueType == ValueType.Boolean ? (bool?)this.Value : null,
                IntValue = this.ValueType == ValueType.Integer ? (int?)this.Value : null,
                DoubleValue = (this.ValueType == ValueType.Double || this.ValueType == ValueType.Float) ? (double?)this.Value : null,
                StringValue = this.ValueType == ValueType.String ? (string)this.Value : null,
                ValueType = this.ValueType,
                UnitType = this.Unit,
            };
        }

    }
}
