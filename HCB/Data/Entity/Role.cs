
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HCB.Data.Interface;

namespace HCB.Data.Entity
{
    [Table("Roles")]
    public class Role : IEntity
    {

        [Required, MaxLength(100)]
        public string Name { get; set; } = "";

        [MaxLength(200)]
        public string? Description { get; set; }

        [Required, MaxLength(200)]
        public string? Password { get; set; } = "";

        // 숫자가 작을수록 상위
        public int Rank { get; set; } = 100;

        public bool IsActive { get; set; } = true;

        public ICollection<RoleScreenAccess> ScreenAccesses { get; set; } = new List<RoleScreenAccess>();
        public ICollection<RoleManageRole> ManageTargets { get; set; } = new List<RoleManageRole>();   // 내가 관리하는 대상들
        public ICollection<RoleManageRole> ManagedBy { get; set; } = new List<RoleManageRole>();
    }
}
