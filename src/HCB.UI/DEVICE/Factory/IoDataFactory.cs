using HCB.Data.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HCB.UI.DEVICE.Factory
{
    public class IoDataFactory
    {
        // =============================================================
        // MotionEntity → DMotion
        // =============================================================
        public static AnalogInput ToRuntime(IoDataEntity e, IIoDevice device)
        {
            var ai = new AnalogInput
            {
                Id = e.Id,
                Name = e.Name,
                Unit = e.Unit,
                Address = e.Address,
                Index = e.Index,
                Description = e.Description,
                IsEnabled = e.IsEnabled,

                Device = device,
            };


            return ai;
        }
    }
}
