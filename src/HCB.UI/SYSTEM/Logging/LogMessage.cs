using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Serilog;

namespace HCB.UI
{

    internal class UILog
    {
        public string Page { get; set; }
        public string User { get; set; }
        public string Message { get; set; }

        public UILog()
        {

        }

        public UILog(string page, string user, string message)
        {
            Page = page;
            User = user;
            Message = message;
        }

        public override string ToString()
        {
            return $"[UI] Page: {Page}, User: {User}, Message: {Message}";
        }
    }
}
