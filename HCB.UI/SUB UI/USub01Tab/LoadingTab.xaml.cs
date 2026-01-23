using HCB.IoC;
using System;
using System.Windows.Controls;

namespace HCB.UI
{

    [View(Lifetime.Scoped)]
    public partial class LoadingTab : UserControl
    {

        public LoadingTab(TableManagerViewModel tableManagerViewModel)
        {
            InitializeComponent();
            DataContext = tableManagerViewModel;
        }


    }
}
