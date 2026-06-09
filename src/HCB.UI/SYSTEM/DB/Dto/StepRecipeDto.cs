using CommunityToolkit.Mvvm.ComponentModel;
using HCB.Data.Entity;

namespace HCB.UI
{
    public partial class StepRecipeDto : ObservableObject
    {
        [ObservableProperty] private int id;
        [ObservableProperty] private int recipeId;
        [ObservableProperty] private string name = "";
        [ObservableProperty] private int stepNumber;
        [ObservableProperty] private int accTime;
        [ObservableProperty] private int accTime2;
        [ObservableProperty] private int contTime;
        [ObservableProperty] private int decTime;
        [ObservableProperty] private double loadCell;
        [ObservableProperty] private double current;
        [ObservableProperty] private double current2;
        [ObservableProperty] private int vacOffTime;
        [ObservableProperty] private string description = "";

        public StepRecipeDto() { }

        private StepRecipeDto(int id, int recipeId, string name, int stepNumber,
            int accTime, int accTime2, int contTime, int decTime,
            double loadCell, double current, double current2, int vacOffTime,
            string description)
        {
            Id = id;
            RecipeId = recipeId;
            Name = name;
            StepNumber = stepNumber;
            AccTime = accTime;
            AccTime2 = accTime2;
            ContTime = contTime;
            DecTime = decTime;
            LoadCell = loadCell;
            Current = current;
            Current2 = current2;
            VacOffTime = vacOffTime;
            Description = description;
        }

        public StepRecipeDto ToDto(StepRecipe entity)
        {
            return new StepRecipeDto(
                entity.Id,
                entity.RecipeId,
                entity.Name,
                entity.StepNumber,
                entity.AccTime,
                entity.AccTime2,
                entity.ContTime,
                entity.DecTime,
                entity.LoadCell,
                entity.Current,
                entity.Current2,
                entity.VacOffTime,
                entity.Description);
        }

        public StepRecipe ToEntity()
        {
            return new StepRecipe
            {
                Id = this.Id,
                RecipeId = this.RecipeId,
                Name = this.Name,
                StepNumber = this.StepNumber,
                AccTime = this.AccTime,
                AccTime2 = this.AccTime2,
                ContTime = this.ContTime,
                DecTime = this.DecTime,
                LoadCell = this.LoadCell,
                Current = this.Current,
                Current2 = this.Current2,
                VacOffTime = this.VacOffTime,
                Description = this.Description
            };
        }
    }
}
