using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.IoC;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace HCB.UI
{
    [ViewModel(Lifetime.Singleton)]
    public partial class USub03ViewModel : ObservableObject
    {
        public UserService UserService { get; }

        public USub03ViewModel(UserService userService)
        {
            this.UserService = userService;

        }

        [RelayCommand]
        public async Task ChangeUser(Authority authority)
        {
            var currentUser = UserService.CurrentAuthority;
            var owner = GetOwnerWindow();
            if (currentUser.Id != authority.Id)
            {
                var dlg1 = new UPasswordNPad(owner, $"{authority.Name} 비밀번호 입력");
                if (dlg1.ShowDialog() != true) return; // 취소
                var pw = dlg1.Password;

                //var result = await UserService.ChangeAuthority(authority, pw);
                //if (result)
                //{
                //    //AlertModal.Ask(owner, "변경", "사용자가 변경되었습니다");
                //}
                //else
                //{
                //    //AlertModal.Ask(owner, "인증 실패", "비밀번호가 틀렸습니다");
                //}
            }
        }

        private static Window? GetOwnerWindow() =>
            Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
            ?? Application.Current?.MainWindow;
    }
}
