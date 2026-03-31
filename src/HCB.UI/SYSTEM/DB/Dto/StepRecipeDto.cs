using CommunityToolkit.Mvvm.ComponentModel;
using HCB.Data.Entity;

namespace HCB.UI
{
    public partial class StepRecipeDto : ObservableObject
    {
        [ObservableProperty] private int id;
        [ObservableProperty] private int recipeId;
        [ObservableProperty] private int stepNumber;
        [ObservableProperty] private double force;
        [ObservableProperty] private double durationTime;
        [ObservableProperty] private string description = "";

        public StepRecipeDto() { }

        private StepRecipeDto(int id, int recipeId, int stepNumber, double force, double durationTime, string description)
        {
            Id = id;
            RecipeId = recipeId;
            StepNumber = stepNumber;
            Force = force;
            DurationTime = durationTime;
            Description = description;
        }

        public StepRecipeDto ToDto(StepRecipe entity)
        {
            return new StepRecipeDto(
                entity.Id,
                entity.RecipeId,
                entity.StepNumber,
                entity.Force,
                entity.DurationTime,
                entity.Description);
        }

        public StepRecipe ToEntity()
        {
            return new StepRecipe
            {
                Id = this.Id,
                RecipeId = this.RecipeId,
                StepNumber = this.StepNumber,
                Force = this.Force,
                DurationTime = this.DurationTime,
                Description = this.Description
            };
        }
    }
}
