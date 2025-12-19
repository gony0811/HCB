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

        public RecipeDto()
        {
        }
        private RecipeDto(int id, string name, bool isActive, ObservableCollection<RecipeParamDto> paramList)
        {
            Id = id;
            Name = name;
            IsActive = isActive;
            ParamList = paramList;
        }

        public RecipeDto ToDto(Recipe entity)
        {
            var paramDtos = new ObservableCollection<RecipeParamDto>();
            foreach (var param in entity.ParamList)
            {
                var paramDto = new RecipeParamDto().ToDto(param);
                paramDtos.Add(paramDto);
            }
            return new RecipeDto(
                entity.Id,
                entity.Name,
                entity.IsActive,
                paramDtos
            );
        }

        public Recipe ToEntity()
        {
            var entity = new Recipe
            {
                Id = this.Id,
                Name = this.Name,
                IsActive = this.IsActive,
                ParamList = new List<RecipeParam>()
            };
            foreach (var paramDto in this.ParamList)
            {
                var paramEntity = paramDto.ToEntity();
                entity.ParamList.Add(paramEntity);
            }
            return entity;
        }
    }
}
