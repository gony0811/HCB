using HCB.Data.Interface;
using System.ComponentModel.DataAnnotations;


namespace HCB.Data.Entity
{
    public sealed class MotionPosition : IEntity
    {

        [Required]
        public string Name { get; set; } = "";

        public double Speed { get; set; } = 0.0;

        public double Position { get; set; } = 0.0;

        public int MotionId { get; set; }

        public MotionEntity? Motion { get; set; }

    }
}
