using CommunityToolkit.Mvvm.ComponentModel;
using HCB.Data.Entity;
using HCB.Data.Repository;
using HCB.IoC;
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

        //private readonly ScreenService _screenService;
        public ObservableCollection<Authority> AuthorityList { get; } = new ObservableCollection<Authority>();

        public ObservableCollection<RoleScreensGroupVM> Groups { get; } = new ObservableCollection<RoleScreensGroupVM>();

        [ObservableProperty]
        private Authority currentAuthority;

        public NavigationViewModel NavVM { get; }

        public UserService(
            RoleRepository rp,
            ScreenRepository sr,
            NavigationViewModel navigationViewModel)
        {
            roleRepository = rp;
            screenRepository = sr;
            NavVM = navigationViewModel;
        }

        public async Task InitializeAsync()
        {
            var roles = await roleRepository.ListAsync();      // 사용 가능한 권한 리스트 불러오기

            AuthorityList.Clear();
            foreach (var r in roles)
            {
                if (r.Name.Equals("OPERATOR") && CurrentAuthority == null)
                {
                    CurrentAuthority = Authority.of(r);
                    await ChangeAuthority(CurrentAuthority, "");
                }
                AuthorityList.Add(Authority.of(r));
            }
        }

        public async Task<bool> ChangeAuthority(Authority authority, string password, CancellationToken ct = default)
        {
            var role = await roleRepository.GetRoleAsync(authority.Name, password, ct);
            if (role is null) return false;
            var allowedScreenCodes = role.ScreenAccesses
                .Where(sa => sa.Screen != null && sa.Screen.IsEnabled)
                .Select(sa => sa.Screen!.Code)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            CurrentAuthority = Authority.of(role);
            NavVM.ApplyScreens(allowedScreenCodes);
            //await ManagedScreen(CurrentAuthority.Id);
            return true;
        }

        // 관리하는 스크린 화면들
        //public async Task ManagedScreen(int managerRoleId, CancellationToken ct = default)
        //{
        //    Groups.Clear();
        //    try
        //    {
        //        var data = await roleRepository.GetManagedRolesScreensAsync(managerRoleId, onlyEnabled: true, ct);
        //        foreach (var g in data) Groups.Add(new RoleScreensGroupVM(managerRoleId, screenRepository, g));
        //    }
        //    catch (Exception e)
        //    {
        //        MessageBox.Show(e.Message);
        //    }


        //}
    }


}
