using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace HCB.UI
{
    public sealed class ScreenItemDto
    {
        public int ScreenId { get; private set; }
        public string Code { get; private set; }
        public string Name { get; private set; }
        public bool Granted { get; private set; }
        public bool IsEnabled { get; private set; }
        public bool CanEdit { get; private set; }

        public ScreenItemDto()
        {
            Code = "";
            Name = "";
        }

        public ScreenItemDto(int screenId, string code, string name, bool granted, bool isEnabled, bool canEdit)
        {
            ScreenId = screenId;
            Code = code ?? "";
            Name = name ?? "";
            Granted = granted;
            IsEnabled = isEnabled;
            CanEdit = canEdit;
        }
    }

    public sealed class RoleScreensGroupDto
    {
        public int RoleId { get; private set; }
        public string RoleName { get; private set; }
        public IReadOnlyList<ScreenItemDto> Screens { get; private set; }

        public int ScreensCount => Screens?.Count ?? 0;

        public RoleScreensGroupDto()
        {
            RoleName = "";
            Screens = Array.AsReadOnly(new ScreenItemDto[0]);
        }

        public RoleScreensGroupDto(int roleId, string roleName, IReadOnlyList<ScreenItemDto> screens)
        {
            RoleId = roleId;
            RoleName = roleName ?? "";
            Screens = screens ?? Array.AsReadOnly(new ScreenItemDto[0]);
        }
    }

    public sealed class RoleScreenFlat
    {
        public int RoleId { get; private set; }
        public string RoleName { get; private set; }
        public int ScreenId { get; private set; }
        public string Code { get; private set; }
        public string Name { get; private set; }
        public bool Granted { get; private set; }
        public bool IsEnabled { get; private set; }
        public bool CanEdit { get; private set; }

        public RoleScreenFlat()
        {
            RoleName = "";
            Code = "";
            Name = "";
        }

        public RoleScreenFlat(int roleId, string roleName, int screenId, string code, string name, bool granted, bool isEnabled, bool canEdit)
        {
            RoleId = roleId;
            RoleName = roleName ?? "";
            ScreenId = screenId;
            Code = code ?? "";
            Name = name ?? "";
            Granted = granted;
            IsEnabled = isEnabled;
            CanEdit = canEdit;
        }
    }
}
