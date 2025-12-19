using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;

namespace HCB.UI
{
    public class RolePermissionRow
    {
        [Key]
        public int RoleId { get; set; }
        public string RoleName { get; set; } = "";
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = "";
        public int PermissionId { get; set; }
        public string PermissionName { get; set; } = "";
        public int Granted { get; set; }     // 0/1
        public int IsEnabled { get; set; }   // 0/1

        public RolePermissionRow() { }
    }
    public sealed class RoleCategoryGroup
    {
        public int RoleId { get; private set; }
        public string RoleName { get; private set; }
        public int CategoryId { get; private set; }
        public string CategoryName { get; private set; }

        public IReadOnlyList<RolePermissionRow> Items { get; private set; }

        public int ItemsCount => Items?.Count ?? 0;

        public RoleCategoryGroup()
        {
            RoleName = "";
            CategoryName = "";
            Items = Array.AsReadOnly(new RolePermissionRow[0]);
        }

        public RoleCategoryGroup(int roleId, string roleName, int categoryId, string categoryName, List<RolePermissionRow> items)
        {
            RoleId = roleId;
            RoleName = roleName ?? "";
            CategoryId = categoryId;
            CategoryName = categoryName ?? "";
            Items = new ReadOnlyCollection<RolePermissionRow>(items ?? new List<RolePermissionRow>());
        }
    }
}