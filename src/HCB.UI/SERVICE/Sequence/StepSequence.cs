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
        public async Task StepMoveWaferCenter(CancellationToken ct)
        {
            try
            {
                _logger.Information("Step Move Wafer Center Start");
                // 여기에 Wafer Center로 이동하는 로직을 구현하세요.

                _sequenceServiceVM.StepWaferCenterMoveCompleted = StepState.InProgress;

                await Task.Delay(3000, ct); // 예시로 3초 대기
            }
            catch (OperationCanceledException)
            {
                _logger.Information("Step Move Wafer Center Canceled");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, ex.Message);
            }
            finally
            {
                _logger.Information("Step Move Wafer Center End");
            }
        }
    }
}
