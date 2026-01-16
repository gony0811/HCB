using CommunityToolkit.Mvvm.ComponentModel;
using HCB.Data.Entity.Type;
using HCB.Data.Entity;

namespace HCB.UI
{
    public partial class MotionCreateVM : ObservableObject
    {
        [ObservableProperty] private string name;
        [ObservableProperty] private int motorNo;
        [ObservableProperty] private double minimumLocation;
        [ObservableProperty] private double maximumLocation;
        [ObservableProperty] private double minimumSpeed;
        [ObservableProperty] private double maximumSpeed;
        [ObservableProperty] private double encoderCountsPerUnit;
        [ObservableProperty] private int hommingProgramNumber;
        [ObservableProperty] private UnitType unit;

        public MotionEntity ToEntity()
        {
            return new MotionEntity
            {
                Name = this.Name,
                MotorNo = this.MotorNo,
                MinimumLocation = this.MinimumLocation,
                MaximumLocation = this.MaximumLocation,
                MinimumSpeed = this.MinimumSpeed,
                MaximumSpeed = this.MaximumSpeed,
                EncoderCountsPerUnit = this.EncoderCountsPerUnit,
                HommingProgramNumber = this.HommingProgramNumber,
                Unit = this.Unit
            };
        }
    }
}
