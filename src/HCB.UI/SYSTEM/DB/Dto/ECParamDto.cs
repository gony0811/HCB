using CommunityToolkit.Mvvm.ComponentModel;
using HCB.Data.Entity;
using HCB.Data.Entity.Type;
using ValueType = HCB.Data.Entity.Type.ValueType;

namespace HCB.UI
{
    public partial class ECParamDto : ObservableObject
    {
        [ObservableProperty] private int id;
        [ObservableProperty] private string name;
        [ObservableProperty] private string value;
        [ObservableProperty] private string maximum;
        [ObservableProperty] private string minimum;
        [ObservableProperty] private UnitType unitType;
        [ObservableProperty] private ValueType valueType;
        [ObservableProperty] private string description;

        public ECParamDto() { }

        private ECParamDto(int id, string name, string value, string maximum, string minimum, UnitType unitType, ValueType valueType, string description)
        {
            Id = id;
            Name = name;
            Value = value;
            Maximum = maximum;
            Minimum = minimum;
            UnitType = unitType;
            ValueType = valueType;
            Description = description;
        }

        public ECParamDto ToDto(ECParam entity)
        {
            return new ECParamDto(
                entity.Id,
                entity.Name,
                entity.Value,
                entity.Maximum,
                entity.Minimum,
                entity.UnitType,
                entity.ValueType,
                entity.Description);
        }

        public ECParam ToEntity()
        {
            return new ECParam
            {
                Id = this.Id,
                Name = this.Name,
                Value = this.Value,
                Maximum = this.Maximum,
                Minimum = this.Minimum,
                UnitType = this.UnitType,
                ValueType = this.ValueType,
                Description = this.Description
            };
        }
    }
}
