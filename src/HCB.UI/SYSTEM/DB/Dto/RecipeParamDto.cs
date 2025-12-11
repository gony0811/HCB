using CommunityToolkit.Mvvm.ComponentModel;
using HCB.Data.Entity;
using HCB.Data.Entity.Type;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ValueType = HCB.Data.Entity.Type.ValueType;

namespace HCB.UI
{
    public partial class RecipeParamDto : ObservableObject
    {
        [ObservableProperty]
        private int id;
        
        [ObservableProperty]
        private string name;
        
        [ObservableProperty]
        private string value;

        [ObservableProperty]
        private string maximum;

        [ObservableProperty]
        private string minimum;

        [ObservableProperty]
        private UnitType unitType;

        [ObservableProperty]
        private ValueType valueType;

        [ObservableProperty]
        private string description;

        [ObservableProperty]
        private int recipeId;

        public RecipeParamDto()
        {

        }

        private RecipeParamDto(int id, int recipeId, string name, string value, string maximum, string minimum, UnitType unitType, ValueType valueType, string description)
        {
            Id = id;
            RecipeId = recipeId;
            Name = name;
            Value = value;
            Maximum = maximum;
            Minimum = minimum;
            UnitType = unitType;
            ValueType = valueType;
            Description = description;
        }

        public RecipeParamDto ToDto(RecipeParam entity)
        {
            return new RecipeParamDto(
                entity.Id,
                entity.RecipeId,
                entity.Name,
                entity.Value,
                entity.Maximum,
                entity.Minimum,
                entity.UnitType,
                entity.ValueType,
                entity.Description);
        }

        public RecipeParam ToEntity()
        {
            return new RecipeParam
            {
                Id = this.Id,
                RecipeId = this.RecipeId,
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
