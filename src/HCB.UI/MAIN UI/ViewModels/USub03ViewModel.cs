using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.Data.Entity;
using HCB.Data.Repository;
using HCB.IoC;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Telerik.Windows.Controls;

namespace HCB.UI
{
    [ViewModel(Lifetime.Singleton)]
    public partial class USub03ViewModel : ObservableObject
    {
        public UserService UserService { get; }
        private DialogService dialogService;
        private RoleScreenRepository roleScreenRepository;

        [ObservableProperty] private ObservableCollection<RoleScreenVM> operatorScreens = new();
        [ObservableProperty] private ObservableCollection<RoleScreenVM> engineerScreens = new();

        [ObservableProperty] private Visibility operatorGrant = Visibility.Collapsed;
        [ObservableProperty] private Visibility engineerGrant = Visibility.Collapsed;
        public USub03ViewModel(UserService userService, DialogService dialogService, RoleScreenRepository roleScreenRepository)
        {
            this.UserService = userService;
            this.dialogService = dialogService;
            this.roleScreenRepository = roleScreenRepository;
            _ = GetScreen();
        }

        [RelayCommand]
        public async Task Login(string username)
        {
            if (UserService.CurrentAuthority.Name == username) return;

            var owner = GetOwnerWindow();
            var dlg1 = new UPasswordNPad(owner, $"{username} 비밀번호 입력");
            if (dlg1.ShowDialog() != true) return; // 취소
            var pw = dlg1.Password;
                
            var result = await UserService.Login(username, pw);

            if (result)
            {
                dialogService.ShowMessage("로그인", "로그인 되었습니다");
                if (UserService.CurrentAuthority.Name == "ADMIN" || UserService.CurrentAuthority.Name == "SERVICE_ENGINEER")
                {
                    OperatorGrant = Visibility.Visible;
                    EngineerGrant = Visibility.Visible; 
                }
                else
                {
                    OperatorGrant = Visibility.Collapsed;
                    EngineerGrant = Visibility.Collapsed;
                }
            }
            else
            {
                dialogService.ShowMessage("로그인 실패", "로그인에 실패했습니다");
            }
        }

        public async Task GetScreen()
        {
            var operatorScreens = await roleScreenRepository.ListAsync(
                predicate: x => x.Role.Name == "OPERATOR", 
                include: i => i.Include(d => d.Screen)
                );

            var engineerScreens = await roleScreenRepository.ListAsync(
                predicate: x => x.Role.Name == "ENGINEER",
                include: i => i.Include(d => d.Screen)
                );

            foreach(var screen in operatorScreens)
            {
                OperatorScreens.Add( new RoleScreenVM
                {
                    RoleId = screen.RoleId,
                    ScreenId = screen.ScreenId,
                    ScreenName = screen.Screen.Name,
                    Granted = screen.Granted
                });
            }

            foreach (var screen in engineerScreens)
            {
                EngineerScreens.Add(new RoleScreenVM
                {
                    RoleId = screen.RoleId,
                    ScreenId = screen.ScreenId,
                    ScreenName = screen.Screen.Name,
                    Granted = screen.Granted
                });
            }
        }

        [RelayCommand]
        public async Task SaveOpScreen()
        {
            try
            {
                var dbScreens = await roleScreenRepository.ListAsync(x => x.Role.Name == "OPERATOR");
                var dbDict = dbScreens.ToDictionary(x => x.ScreenId);

                List<RoleScreenAccess> updateList = new();

                foreach (var vmScreen in OperatorScreens)
                {
                    if (dbDict.TryGetValue(vmScreen.ScreenId, out var dbEntry))
                    {
                        // 값이 실제로 바뀌었을 때만 업데이트 리스트에 추가 (DB 부하 감소)
                        if (dbEntry.Granted != vmScreen.Granted)
                        {
                            dbEntry.Granted = vmScreen.Granted;
                            updateList.Add(dbEntry);
                        }
                    }
                }

                // 3. 변경된 데이터가 있을 경우에만 일괄 업데이트
                if (updateList.Any())
                {
                    await roleScreenRepository.UpdateRange(updateList);
                    dialogService.ShowMessage("저장 완료", "운영자 권한 설정이 저장되었습니다.");
                }
            }
            catch (Exception ex)
            {
                dialogService.ShowMessage("저장 실패", $"오류가 발생했습니다: {ex.Message}");
            }
        }

        [RelayCommand]
        public async Task SaveENGScreen()
        {
            try
            {
                var dbScreens = await roleScreenRepository.ListAsync(x => x.Role.Name == "ENGINEER");
                var dbDict = dbScreens.ToDictionary(x => x.ScreenId);

                List<RoleScreenAccess> updateList = new();

                foreach (var vmScreen in OperatorScreens)
                {
                    if (dbDict.TryGetValue(vmScreen.ScreenId, out var dbEntry))
                    {
                        // 값이 실제로 바뀌었을 때만 업데이트 리스트에 추가 (DB 부하 감소)
                        if (dbEntry.Granted != vmScreen.Granted)
                        {
                            dbEntry.Granted = vmScreen.Granted;
                            updateList.Add(dbEntry);
                        }
                    }
                }

                // 3. 변경된 데이터가 있을 경우에만 일괄 업데이트
                if (updateList.Any())
                {
                    await roleScreenRepository.UpdateRange(updateList);
                    dialogService.ShowMessage("저장 완료", "운영자 권한 설정이 저장되었습니다.");
                }
            }
            catch (Exception ex)
            {
                dialogService.ShowMessage("저장 실패", $"오류가 발생했습니다: {ex.Message}");
            }
        }

        private static Window? GetOwnerWindow() =>
            Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
            ?? Application.Current?.MainWindow;
    }
}
