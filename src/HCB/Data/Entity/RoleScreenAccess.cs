
namespace HCB.Data.Entity
{
    public class RoleScreenAccess
    {
        public int RoleId { get; set; }
        public int ScreenId { get; set; }
        public bool Granted { get; set; } = true;

        public Role? Role { get; set; }
        public Screen? Screen { get; set; }
    }
}
