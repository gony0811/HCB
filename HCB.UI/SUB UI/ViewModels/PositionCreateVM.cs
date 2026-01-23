using CommunityToolkit.Mvvm.ComponentModel;
using HCB.Data.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HCB.UI
{
    public partial class PositionCreateVM : ObservableObject
    {
        [ObservableProperty] private string name;
        [ObservableProperty] private double position;
        [ObservableProperty] private double speed;

        public MotionPosition ToEntity(int motionId)
        {
            return new MotionPosition
            {
                Name = this.Name,
                Position = this.Position,
                Speed = this.Speed,
                MotionId = motionId
            };
        }
    }
}
