namespace HCB.Data.Entity
{
    public class RoleManageRole
    {
        public int ManagerRoleId { get; set; }
        public int TargetRoleId { get; set; }
        public bool CanManage { get; set; } = true;

        public Role? Manager { get; set; }
        public Role? Target { get; set; }
    }
}
