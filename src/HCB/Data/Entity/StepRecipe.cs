using HCB.Data.Interface;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCB.Data.Entity
{
    [Table("StepRecipe")]
    public class StepRecipe : IEntity
    {
        public int RecipeId { get; set; }

        [MaxLength(100)]
        public string Name { get; set; } = "";

        public int StepNumber { get; set; }

        public int AccTime { get; set; }

        public int AccTime2 { get; set; }

        public int ContTime { get; set; }

        public int DecTime { get; set; }

        public double LoadCell { get; set; }

        public double Current { get; set; }

        public double Current2 { get; set; }

        public int VacOffTime { get; set; }

        [MaxLength(200)]
        public string Description { get; set; } = "";

        public Recipe? Recipe { get; set; }
    }
}
