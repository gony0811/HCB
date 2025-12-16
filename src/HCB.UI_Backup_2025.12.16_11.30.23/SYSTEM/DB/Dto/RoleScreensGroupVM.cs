using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HCB.Data.Repository;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;

namespace HCB.UI
{
    public class RoleScreensGroupVM
    {
        public int RoleId { get; }
        public string RoleName { get; }
        public ObservableCollection<ScreenItemVM> Screens { get; } = new ObservableCollection<ScreenItemVM>();
        public RoleScreensGroupVM(int managerRoleId, ScreenRepository repository, RoleScreensGroupDto dto)
        {
            RoleId = dto.RoleId;
            RoleName = dto.RoleName;
            foreach (var s in dto.Screens)
            {
                Screens.Add(new ScreenItemVM(repository, managerRoleId,
                    targetRoleId: dto.RoleId,
                    screenId: s.ScreenId, code: s.Code, name: s.Name,
                    granted: s.Granted, isEnabled: s.IsEnabled, canEdit: s.CanEdit));
            }
        }
    }

    public partial class ScreenItemVM : ObservableObject
    {
        private readonly ScreenRepository _repo;
        private readonly int _managerRoleId;

        public int TargetRoleId { get; }
        public int ScreenId { get; }
        public string Code { get; }
        public string Name { get; }

        [ObservableProperty] private bool granted;
        public bool IsEnabled { get; }
        public bool CanEdit { get; }
        public bool CanToggle => IsEnabled && CanEdit && !Busy;

        [ObservableProperty] private bool busy;

        public ScreenItemVM(ScreenRepository repo, int managerRoleId,
                            int targetRoleId, int screenId, string code, string name,
                            bool granted, bool isEnabled, bool canEdit)
        {
            _repo = repo;
            _managerRoleId = managerRoleId;

            TargetRoleId = targetRoleId;
            ScreenId = screenId;
            Code = code;
            Name = name;
            Granted = granted;
            IsEnabled = isEnabled;
            CanEdit = canEdit;
        }

        // ToggleButton에서 현재 체크값을 파라미터로 받음 (true/false)
        [RelayCommand]
        public async Task ToggleAsync()
        {
            if (!CanToggle) return;

            var desired = !Granted;

            Busy = true;
            try
            {
                var ok = await _repo.SetGrantAsync(_managerRoleId, TargetRoleId, ScreenId, desired);
                if (ok) Granted = desired;
            }catch(Exception e )
            {
                MessageBox.Show(e.Message);
            }
            
            Busy = false;

            OnPropertyChanged(nameof(CanToggle));
        }
    }

}
