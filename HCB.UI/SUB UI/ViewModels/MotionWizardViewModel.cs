using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Xml.Linq;
using Telerik.Windows.Documents.Fixed.Model.Data;
using Telerik.Windows.Documents.Spreadsheet.Model;

namespace HCB.UI
{
    public enum WizardStep
    {
        Name,
        Speed,
        Position,
        Finish
    }

    public class MotionWizardResult
    {
        public string Name { get; set; }
        public double Speed { get; set; }
        public double Position { get; set; }
    }

    public partial class MotionWizardViewModel : ObservableObject
    {
        [ObservableProperty] private WizardStep currentStep = WizardStep.Name;

        // 결과값
        [ObservableProperty] private string name;
        [ObservableProperty] private double speed;
        [ObservableProperty] private double position;

        public MotionWizardResult Result =>
            new MotionWizardResult
            {
                Name = Name,
                Speed = Speed,
                Position = Position
            };

        // ======================
        // Step 이동
        // ======================
        [RelayCommand]
        private void Next()
        {
            if (CurrentStep < WizardStep.Finish)
                CurrentStep++;
        }

        [RelayCommand]
        private void Back()
        {
            if (CurrentStep > WizardStep.Name)
                CurrentStep--;
        }
    }
}
