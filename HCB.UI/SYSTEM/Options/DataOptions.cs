using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HCB.UI
{
    public class DataOptions
    {
        internal const string Data = "Data";

        public string Db { get; set; } = string.Empty;

        public bool Simulation { get; set; } = false;
    }

}
