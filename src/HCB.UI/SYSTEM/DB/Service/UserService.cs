using CommunityToolkit.Mvvm.ComponentModel;
using HCB.Data.Entity;
using HCB.Data.Repository;
using HCB.IoC;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace HCB.UI
{
    [Service(Lifetime.Singleton)]
    public partial class UserService : ObservableObject
    {
        private readonly RoleRepository roleRepository;
        private readonly ScreenRepository screenRepository;
        private readonly RoleScreenRepository roleScreenRepository;

        //private readonly ScreenService _screenService;
        public ObservableCollection<Authority> AuthorityList { get; } = new ObservableCollection<Authority>();

        public ObservableCollection<RoleScreensGroupVM> Groups { get; } = new ObservableCollection<RoleScreensGroupVM>();

        [ObservableProperty] private Authority currentAuthority;

        public NavigationViewModel NavVM { get; }

        public UserService(
            RoleRepository rp,
            ScreenRepository sr,
            NavigationViewModel navigationViewModel, 
            RoleScreenRepository roleScreenRepository)
        {
            roleRepository = rp;
            screenRepository = sr;
            NavVM = navigationViewModel;
            this.roleScreenRepository = roleScreenRepository;
            
        }

        public async Task InitializeAsync()
        {
            try
            {
                var roles = await roleRepository.ListAsync();
                var role = roles.FirstOrDefault(r => r.Name == "OPERATOR");
                CurrentAuthority = Authority.of(role);
                await LoadRoleScreen();
            }
            catch (Exception ex)
            {

            }
        }
        public async Task<bool> Login(string username, string pwd, CancellationToken ct = default)
        {
            try
            {
                var entity = await roleRepository.ListAsync(x => x.Name == username);
                var role = entity.FirstOrDefault();
                if (role is null) throw new Exception("Role not found");
                if (role.Password != pwd) throw new Exception("Invalid password");
                CurrentAuthority = Authority.of(role);
                await LoadRoleScreen();
                return true;
            }
            catch (Exception ex)
            {

                return false;
            }
        }

        public async Task LoadRoleScreen()
        {
            if (CurrentAuthority == null) return;
            NavVM.MainEnabled = true;
            NavVM.RecipeEnabled = true;
            NavVM.ParameterEnabled = true;
            NavVM.UserEnabled = true;
            NavVM.LogEnabled = true;
            NavVM.AlarmEnabled = true;
            NavVM.MotionEnabled = true;
            NavVM.IOEnabled = true;
            NavVM.DeviceEnabled = CurrentAuthority.Name == "SERVICE_ENGINEER" ? Visibility.Visible : Visibility.Collapsed;
        }
    }

}

