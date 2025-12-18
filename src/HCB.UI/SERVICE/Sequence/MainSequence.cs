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
                this._sequenceServiceVM.StatusMessage = "Auto Run Start";

                await Task.Delay(3000, ct);

                this._sequenceServiceVM.StatusMessage = "Auto Run Completed";

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
        }

    }
}
