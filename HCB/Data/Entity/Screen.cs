
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using HCB.Data.Interface;

namespace HCB.Data.Entity
{
    public class Screen : IEntity
    {
        public string? Code { get; set; } = "";
        public string? Name { get; set; } = "";
        public string? Path { get; set; }
        public int DisplayOrder { get; set; } = 0;
        public bool IsEnabled { get; set; } = true;

        public ICollection<RoleScreenAccess> AccessBy { get; set; } = new List<RoleScreenAccess>();
    }
}
