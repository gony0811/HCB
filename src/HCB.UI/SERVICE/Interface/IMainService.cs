using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HCB.UI
{
    public interface IMainService
    {
        Task StartAsync();
        Task StopAsync();
    }
}
