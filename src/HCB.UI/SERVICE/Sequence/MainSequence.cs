using HCB.Data.Entity.Type;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HCB.UI
{
    public partial class SequenceService
    {
        private int step = 0;

        public async Task MainSequence()
        {
            try
            {
                switch(step)
                {
                    // Step 0: 장비 상태 초기화
                    case 0:
                        _logger.Debug("장비 상태 초기화");
                        EQStatus.Availability = Availability.Up;
                        EQStatus.Operation = OperationMode.Manual;
                        EQStatus.Run = RunStop.Stop;
                        EQStatus.Alarm = AlarmLevel.Normal;
                        step++;
                        break;
                    case 1:

                        

                        break;
                }

                
            }
            catch (OperationCanceledException)
            {
                _logger.Information("InitialSequenceAsync: 초기화 시퀀스가 취소되었습니다.");

                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "InitialSequenceAsync: 초기화 시퀀스 중 오류 발생");
                throw;
            }
        }
    }
}
