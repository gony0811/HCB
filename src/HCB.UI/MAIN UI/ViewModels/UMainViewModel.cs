
using Autofac;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.IoC;
using System.Windows.Controls;

namespace HCB.UI
{
    [ViewModel(Lifetime.Singleton)]
    public partial class UMainViewModel : ObservableObject
    {

        //[ObservableProperty] private ObservableCollection<string> logMessages = new ObservableCollection<string>();

        private UserService UserService;

        private NavigationViewModel NavVM;

        [ObservableProperty] public Page currentPage;

        [ObservableProperty] private string selectedPageKey;
        //public string selectedPageKey { get; set; }

        public UMainViewModel(UserService userService, NavigationViewModel navVM)
        {
            this.UserService = userService;
            this.NavVM = navVM;
            _ = UserService.InitializeAsync();
            Navigate("Main");

        }

        [RelayCommand]
        public void Navigate(string key)
        {
            key = key.ToUpper();
            SelectedPageKey = key;

            switch(key)
            {
                case "MAIN":
                    CurrentPage = App.Container.Resolve<USub01>();
                    break;
                case "PARAMETER":
                    //CurrentPage = App.Container.Resolve<USub02>();
                    break;
                case "USER":
                    //CurrentPage = App.Container.Resolve<USub03>();
                    break;
                case "LOG":
                    //CurrentPage = App.Container.Resolve<USub04>();
                    break;
                case "ALARM":
                    //CurrentPage = App.Container.Resolve<USub05>();
                    break;
                case "MOTION":
                    //CurrentPage = App.Container.Resolve<USub06>();
                    break;
                case "IO":
                    //CurrentPage = App.Container.Resolve<USub07>();
                    break;
                case "DEVICE":
                    CurrentPage = App.Container.Resolve<USub08>();
                    break;
                default:
                    //CurrentPage = App.Container.Resolve<USub01>();
                    break;
            }
        }
    }
}
