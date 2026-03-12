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
        public async Task MachineStartAsync(int topDie, int btmDie, CancellationToken ct)
        {
            try
            {
                _logger.Information("Auto Run Start");

                var Status = _operationService.Status;

                if (Status.Availability == Availability.Down)
                {
                    _logger.Warning("Cannot execute MachineStartAsync: Sequence Service is not in Auto Standby Status.");
                    return;
                }

                // 1. Btm Die 
                // #1. BTM DIE 가 놓인 VACUUM위치로 저배율 카메라를 이동시킨다.
                var BtmDieAlign = await DTableCarrierAlign(btmDie, ct);
                await DTableBTMPickup(btmDie, BtmDieAlign, ct);
                await BtmDieDrop(1, ct);
                await Init_Head(ct);

                ////2.TopDie
                var TopDieAlign = await DTableCarrierAlign(topDie, ct);
                await DTableTOPPickup(topDie, TopDieAlign, ct);

                //// 3. 고배율 보정
                var topDieVisionResults = await TopDieVision(ct);
                //await TopDieDrop(ct);
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


        public async Task DieAlignAndPick(int dVac, CancellationToken ct)
        {
            try
            {
                var BtmDieAlign = await DTableCarrierAlign(dVac, ct);
                await DTableBTMPickup(dVac, BtmDieAlign, ct);
            }catch(Exception e)
            {
                _logger.Error(e.Message);
            }
        }

        public async Task BTMPlace(CancellationToken ct)
        {
            await BtmDieDrop(1, ct);
            await Init_Head(ct);
        }

        public async Task TopDieAlignAndPick(int dVac, CancellationToken ct)
        {
            try
            {
                var topDieAlign = await DTableCarrierAlign(dVac, ct);
                await DTableTOPPickup(dVac, topDieAlign, ct);
            }
            catch (Exception e)
            {
                _logger.Error(e.Message);
            }
        }

    }
}
