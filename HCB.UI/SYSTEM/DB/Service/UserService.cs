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
            var groups = await roleScreenRepository.ListAsync(predicate: x=> x.RoleId == CurrentAuthority.Id, include: query => query.Include(d => d.Screen));
            NavVM.MainEnabled = groups.FirstOrDefault(groups => groups.Screen.Code == "MAIN")?.Granted ?? false;
            NavVM.ParameterEnabled = groups.FirstOrDefault(groups => groups.Screen.Code == "PARAMETER")?.Granted ?? false;
            NavVM.UserEnabled= groups.FirstOrDefault(groups => groups.Screen.Code == "USER")?.Granted ?? false;
            NavVM.LogEnabled = groups.FirstOrDefault(groups => groups.Screen.Code == "LOG")?.Granted ?? false;
            NavVM.AlarmEnabled = groups.FirstOrDefault(groups => groups.Screen.Code == "ALARM")?.Granted ?? false;
            NavVM.MotionEnabled = groups.FirstOrDefault(groups => groups.Screen.Code == "MOTION")?.Granted ?? false;
            NavVM.IOEnabled = groups.FirstOrDefault(groups => groups.Screen.Code == "IO")?.Granted ?? false;
            NavVM.DeviceEnabled = CurrentAuthority.Name == "SERVICE_ENGINEER" ? Visibility.Visible : Visibility.Collapsed;
        }
    }

}

