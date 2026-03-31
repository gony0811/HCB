using CommunityToolkit.Mvvm.ComponentModel;
using HCB.Data.Entity;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace HCB.UI
{
    public partial class RecipeDto : ObservableObject
    {
        [ObservableProperty]
        private int id;

        [ObservableProperty]
        private string name;

        [ObservableProperty]
        private bool isActive;

        [ObservableProperty]
        private ObservableCollection<RecipeParamDto> paramList = new ObservableCollection<RecipeParamDto>();

        [ObservableProperty]
        private ObservableCollection<StepRecipeDto> stepList = new ObservableCollection<StepRecipeDto>();

        public RecipeDto()
        {
        }
        private RecipeDto(int id, string name, bool isActive, ObservableCollection<RecipeParamDto> paramList, ObservableCollection<StepRecipeDto> stepList)
        {
            Id = id;
            Name = name;
            IsActive = isActive;
            ParamList = paramList;
            StepList = stepList;
        }

        public RecipeDto ToDto(Recipe entity)
        {
            var paramDtos = new ObservableCollection<RecipeParamDto>();
            foreach (var param in entity.ParamList)
            {
                paramDtos.Add(new RecipeParamDto().ToDto(param));
            }
            var stepDtos = new ObservableCollection<StepRecipeDto>();
            foreach (var step in entity.StepList)
            {
                stepDtos.Add(new StepRecipeDto().ToDto(step));
            }
            return new RecipeDto(
                entity.Id,
                entity.Name,
                entity.IsActive,
                paramDtos,
                stepDtos
            );
        }

        public Recipe ToEntity()
        {
            var entity = new Recipe
            {
                Id = this.Id,
                Name = this.Name,
                IsActive = this.IsActive,
                ParamList = new List<RecipeParam>(),
                StepList = new List<StepRecipe>()
            };
            foreach (var paramDto in this.ParamList)
            {
                entity.ParamList.Add(paramDto.ToEntity());
            }
            foreach (var stepDto in this.StepList)
            {
                entity.StepList.Add(stepDto.ToEntity());
            }
            return entity;
        }
    }
}
