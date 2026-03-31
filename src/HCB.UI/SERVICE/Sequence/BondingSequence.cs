using HCB.UI.SERVICE;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using static HCB.UI.SERVICE.CalibrationService;

namespace HCB.UI
{
    public partial class SequenceService : BackgroundService
    {

        
        // 아래는 TopDieDrop 함수의 기존 버전입니다. 위의 버전은 테스트를 위해 만들어진 버전입니다.
        public async Task<(double moveX, double moveY, double moveTheta)> TopDieDrop(Dictionary<string, VisionMarkResult> topVisionMarkResult, CancellationToken ct)
        {
            VisionMarkResult rightFid = new VisionMarkResult
            {
                MarkType = MarkType.FIDUCIAL,
                DirectType = DirectType.RIGHT,
                StageX = await GetPosition(MotionExtensions.H_X, "PLACE_CENTER", ct),
                StageY = await GetPosition(MotionExtensions.W_Y, "PLACE_CENTER", ct),
            };

            VisionMarkResult rightAlign = new VisionMarkResult
            {
                MarkType = MarkType.ALIGN_MARK,
                DirectType = DirectType.RIGHT,
                StageX = await GetPosition(MotionExtensions.H_X, "PLACE_CENTER", ct),
                StageY = await GetPosition(MotionExtensions.W_Y, "PLACE_CENTER", ct),
            };

            VisionMarkResult leftFid = new VisionMarkResult
            {
                MarkType = MarkType.FIDUCIAL,
                DirectType = DirectType.LEFT,
                StageX = await GetPosition(MotionExtensions.H_X, "PLACE_CENTER", ct),
                StageY = await GetPosition(MotionExtensions.W_Y, "PLACE_CENTER", ct),
            };

            VisionMarkResult leftAlign = new VisionMarkResult
            {
                MarkType = MarkType.ALIGN_MARK,
                DirectType = DirectType.LEFT,
                StageX = await GetPosition(MotionExtensions.H_X, "PLACE_CENTER", ct),
                StageY = await GetPosition(MotionExtensions.W_Y, "PLACE_CENTER", ct),
            };

            if (double.TryParse(_recipeService.FindByParam("TopDieThickness").Value, out double topDieThickness))
            { }
            else
            {
                throw new Exception("레시피 TopDieThickness값이 Double타입이 아닙니다");
            }

            if (double.TryParse(_recipeService.FindByParam("BtmDieThickness").Value, out double btmDieThickness))
            { }
            else
            {
                throw new Exception("레시피 btmDieThickness값이 Double타입이 아닙니다");
            }

            if (double.TryParse(_recipeService.FindByParam("CenterToHC2OffsetX").Value, out double CenterToHC2OffsetX))
            { }
            else
            {
                throw new Exception("레시피 CenterToHC2OffsetX값이 Double타입이 아닙니다");
            }

            if (double.TryParse(_recipeService.FindByParam("CenterToHC2OffsetY").Value, out double CenterToHC2OffsetY))
            { }
            else
            {
                throw new Exception("레시피 CenterToHC2OffsetY값이 Double타입이 아닙니다");
            }

            // W-Table로 이동
            await Init_Head(ct);
            await MotionsMove([MotionExtensions.H_X, MotionExtensions.W_Y], "PLACE_CENTER", ct);
            await MotionsMove(MotionExtensions.H_Z, "PLACE_STANBY", -topDieThickness - btmDieThickness, ct);

            await communicationService.RequestAFStart(CameraType.HC1_HIGH, MarkType.FIDUCIAL, ct);
            var lFid = await communicationService.RequestVisionMarkPosition(MarkType.FIDUCIAL, CameraType.HC1_HIGH, DirectType.LEFT.ToString());
            if (lFid.Result == Result.NG) throw new Exception("Left Fiducial 측정 실패");
            leftFid.DxCamToMark = lFid.X;
            leftFid.DyCamToMark = lFid.Y;

            await communicationService.RequestAFStart(CameraType.HC1_HIGH, MarkType.ALIGN_MARK, ct);
            var lAlign = await communicationService.RequestVisionMarkPosition(MarkType.ALIGN_MARK, CameraType.HC1_HIGH, DirectType.LEFT.ToString());
            if (lAlign.Result == Result.NG) throw new Exception("Left Align 측정 실패");
            leftAlign.DxCamToMark = lAlign.X;
            leftAlign.DyCamToMark = lAlign.Y;

            await communicationService.RequestAFStart(CameraType.HC2_HIGH, MarkType.FIDUCIAL, ct);
            var rFid = await communicationService.RequestVisionMarkPosition(MarkType.FIDUCIAL, CameraType.HC2_HIGH, DirectType.RIGHT.ToString());
            if (rFid.Result == Result.NG) throw new Exception("Right Fiducial측정 실패");
            rightFid.DxCamToMark = rFid.X;
            rightFid.DyCamToMark = rFid.Y;

            await communicationService.RequestAFStart(CameraType.HC2_HIGH, MarkType.ALIGN_MARK, ct);
            var rAlign = await communicationService.RequestVisionMarkPosition(MarkType.ALIGN_MARK, CameraType.HC2_HIGH, DirectType.RIGHT.ToString());
            if (rAlign.Result == Result.NG) throw new Exception("Right Align 측정 실패");
            rightAlign.DxCamToMark = rAlign.X;
            rightAlign.DyCamToMark = rAlign.Y;

            var fidToDie = CalibrationService.FidToDie(topVisionMarkResult["RIGHT_FID"], topVisionMarkResult["RIGHT_ALIGN"]);
            var dTheta_die = CalibrationService.CalcTheta(
                    topVisionMarkResult["RIGHT_FID"],
                    topVisionMarkResult["LEFT_FID"]
            ) - CalibrationService.CalcTheta(
                    topVisionMarkResult["RIGHT_ALIGN"],
                    topVisionMarkResult["LEFT_ALIGN"]
            );

            var fidToWafer = CalibrationService.FidToWafer(rightFid, rightAlign);
            var dTheta_wafer = CalibrationService.WaferCalcTheta(rightFid, leftFid, CenterToHC2OffsetX, CenterToHC2OffsetY) - CalibrationService.WaferCalcTheta(rightAlign, leftAlign, CenterToHC2OffsetX, CenterToHC2OffsetY);

            var xyt = CalculateRelativeBondingMove(
                fidToDie.X, fidToDie.Y, dTheta_die,
                fidToWafer.X, fidToWafer.Y, dTheta_wafer
            );

            await Task.WhenAll(
                _sequenceHelper.RelativeMoveAsync(MotionExtensions.H_X, 0, xyt.moveX, ct),
                _sequenceHelper.RelativeMoveAsync(MotionExtensions.W_Y, 0, xyt.moveY, ct),
                _sequenceHelper.RelativeMoveAsync(MotionExtensions.H_T, 0, xyt.moveTheta, ct)
            );
            //await Pressurize();
            await MotionsMove(MotionExtensions.H_Z, "DIE_PLACE", -topDieThickness - btmDieThickness, ct);
            bool result = await _sequenceHelper.HeadPickerVacuum(eOnOff.Off, ct);
            await MotionsMove(MotionExtensions.H_Z, "DIE_PLACE", -topDieThickness - btmDieThickness - 1, ct);
            await Init_Head(ct);

            //await _sequenceHelper.RelativeMoveAsync(MotionExtensions.W_Y, 0, xyt.moveY, ct);

            if (!result) throw new Exception("HeadPicker를 확인해주세요");
            return xyt;
        }

        // 가압 시퀀스
        public async Task Pressurize(int force=2090, int delayMs = 1300)
        {
            var device = _deviceManager.GetDevice<PowerPmacDevice>("PMAC");
            string forceSet = $"P8276={force}";
            string command = "ENABLE PLC 11";
            await device.SendCommand(forceSet);
            await device.SendCommand(command);
            await Task.Delay(delayMs);
        }

        public (double moveX, double moveY, double moveTheta) CalculateRelativeBondingMove(
            double dx_fidToDie, double dy_fidToDie, double dTheta_die, // P-table 데이터
            double dx_fidToWafer, double dy_fidToWafer, double theta_wafer // W-table 데이터
        )
        {
            // Y 
            if (double.TryParse(_recipeService.FindByParam("ShankToHeadOffsetY").Value, out double _dx_cal))
            { }
            else
            {
                throw new Exception("레시피 ShankLowOffsetY값이 Double타입이 아닙니다");
            }

            if (double.TryParse(_recipeService.FindByParam("ShankToHeadOffsetX").Value, out double _dy_cal))
            { }
            else
            {
                throw new Exception("레시피 ShankLowOffsetY값이 Double타입이 아닙니다");
            }


            // 1. 회전 중심 기준 통합 벡터 구성
            double dx_total = _dx_cal + dx_fidToDie;
            //double dy_total = _dy_cal + (dy_fidToDie * _yScale_pw);
            double dy_total = _dy_cal + (dy_fidToDie);

            // 2. 최종 회전량 및 Shift 계산
            double finalTheta = theta_wafer + dTheta_die;
            double rad = finalTheta * (Math.PI / 180.0);
            double cosT = Math.Cos(rad);
            double sinT = Math.Sin(rad);

            double deltaX = dx_total * (cosT - 1) - dy_total * sinT;
            double deltaY = dx_total * sinT + dy_total * (cosT - 1);

            // 3. 보정 후 피디셜 대비 Die의 상대 위치 계산
            double vDieAfterRotX = (dx_total - _dx_cal) + deltaX;
            double vDieAfterRotY = (dy_total - _dy_cal) + deltaY;

            // 4. 헤드 피디셜 위치에서 추가로 이동해야 할 상대 거리
            // (이미 피디셜을 웨이퍼 마크 근처에 가져다 놓았다고 가정할 때의 미세 보정량)
            double finalMoveX = dx_fidToWafer - vDieAfterRotX;
            double finalMoveY = dy_fidToWafer - vDieAfterRotY;

            return (finalMoveX, finalMoveY, finalTheta);
        }




    }
}
