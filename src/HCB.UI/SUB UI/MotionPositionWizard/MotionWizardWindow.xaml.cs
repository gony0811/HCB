using Telerik.Windows.Controls;

namespace HCB.UI
{
    public partial class MotionWizardWindow : RadWindow
    {
        public MotionWizardResult Result { get; private set; }

        public MotionWizardWindow()
        {
            InitializeComponent();
            //DataContext = new MotionWizardViewModel();
        }

        private void Wizard_Finish(object sender, NavigationButtonsEventArgs e)
        {
            //Result = ((MotionWizardViewModel)DataContext).Result;
            DialogResult = true;
            Close();
        }

        private void Wizard_Cancel(object sender, NavigationButtonsEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
