using HCB.Data.Entity;
using HCB.Data.Entity.Type;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HCB.UI
{
    public class IoDataFactory
    {
        // =============================================================
        // MotionEntity → DMotion
        // =============================================================
        public static IIoData ToRuntime(IoDataEntity e, IIoDevice device)
        {
            if (e.IoDataType == IoType.DigitalInput)
            {
                return ToDigitalInputRuntime(e, device);
            }
            else if (e.IoDataType == IoType.DigitalOutput)
            {
                return ToDigitalOutputRuntime(e, device);
            }
            else if (e.IoDataType == IoType.AnalogInput)
            {
                return ToAnalogInputRuntime(e, device);
            }
            else if (e.IoDataType == IoType.AnalogOutput)
            {
                return ToAnalogOutputRuntime(e, device);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private static IIoData ToAnalogOutputRuntime(IoDataEntity e, IIoDevice device)
        {
            return new AnalogOutput
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
        }

        private static IIoData ToAnalogInputRuntime(IoDataEntity e, IIoDevice device)
        {
            return new AnalogInput
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
        }

        private static IIoData ToDigitalOutputRuntime(IoDataEntity e, IIoDevice device)
        {
            return new DigitalOutput
            {
                Id = e.Id,
                Name = e.Name,
                Address = e.Address,
                Index = e.Index,
                Description = e.Description,
                IsEnabled = e.IsEnabled,
                Device = device,
            };
        }

        private static IIoData ToDigitalInputRuntime(IoDataEntity e, IIoDevice device)
        {
            return new DigitalInput
            {
                Id = e.Id,
                Name = e.Name,
                Address = e.Address,
                Index = e.Index,
                Description = e.Description,
                IsEnabled = e.IsEnabled,
                Device = device,
            };
        }

        public static IoDataEntity ToEntity(IIoData io)
        {
            if (io is DigitalInput d_i)
            {
                return ToDigitalEntity(d_i);
            }
            else if (io is DigitalOutput d_o)
            {
                return ToDigitalEntity(d_o);
            }
            else if (io is AnalogInput a_i)
            {
                return ToAnalogEntity(a_i);
            }
            else if (io is AnalogOutput a_o)
            {
                return ToAnalogEntity(a_o);
            }
            else
            {
                throw new NotImplementedException();
            }

            
        }

        private static IoDataEntity ToAnalogEntity(AbstractAnalog io)
        {
            var e = new IoDataEntity
            {
                Id = io.Id,
                Name = io.Name,
                Address = io.Address,
                Index = io.Index,
                IsEnabled = io.IsEnabled, // 런타임 double → DB bool

                IoDataType = io.IoType,
                Unit = io.Unit,
                
                Description = io.Description,
                ParentDeviceId = io.Device.Id
            };

            return e;
        }

        private static IoDataEntity ToDigitalEntity(AbstractDigital io)
        {
            var e = new IoDataEntity
            {
                Id = io.Id,
                Name = io.Name,
                Address = io.Address,
                Index = io.Index,
                IsEnabled = io.IsEnabled, // 런타임 double → DB bool

                IoDataType = io.IoType,

                Description = io.Description,
                ParentDeviceId = io.Device.Id
            };

            return e;
        }
    }
}
