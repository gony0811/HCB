using HCB.Data.Entity.Type;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HCB.UI
{
    public partial class SequenceService : BackgroundService
    {


        public async Task MachineStartAsync(CancellationToken ct)
        {
            try
            {
                _logger.Information("Auto Run Start");

                if (EQStatus.Availability == Availability.Down || EQStatus.Run == RunStop.Run || EQStatus.Operation == OperationMode.Manual || EQStatus.Alarm == AlarmLevel.HEAVY)
                {
                    _logger.Warning("Cannot execute MachineStartAsync: Sequence Service is not in Auto Standby Status.");
                    return;
                }

                await Task.Delay(3000, ct);
            }
            catch (OperationCanceledException)
            {
                _logger.Information("Auto Run Canceled");

                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, ex.Message);
                throw;
            }
            finally
            {
                _logger.Information("Auto Run End");
            }
        }

    }
}
