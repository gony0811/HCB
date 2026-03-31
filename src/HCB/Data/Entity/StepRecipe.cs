using HCB.Data.Interface;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCB.Data.Entity
{
    [Table("StepRecipe")]
    public class StepRecipe : IEntity
    {
        public int RecipeId { get; set; }

        public int StepNumber { get; set; }

        public double Force { get; set; }

        public double DurationTime { get; set; }

        [MaxLength(200)]
        public string Description { get; set; } = "";

        public Recipe? Recipe { get; set; }
    }
}
