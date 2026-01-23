using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HCB.Data.Interface
{
    public abstract class IEntity
    {
        [Key]
        public int Id { get; set; }
    }
}
