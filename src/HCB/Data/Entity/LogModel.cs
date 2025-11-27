using HCB.Data.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HCB.Data.Entity 
{
    public class LogModel : IEntity
    {
        // 포맷: yyMMddTHH:mm:ss.fffffffz
        public string? Timestamp { get; set; }
        public string? Level { get; set; }
        public string? Message { get; set; }
        public string? Exception { get; set; }
    }
}
