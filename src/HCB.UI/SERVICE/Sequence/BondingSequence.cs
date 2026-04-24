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
        /// <summary>
        /// Top Die 를 W-Table 위로 배치한 후 Hc 카메라 4점 측정 → Pc 측정값과 함께
        /// 최종 본딩 이동량을 계산하고 실행한다.
        /// </summary>
        public async Task<(double moveX, double moveY, double moveTheta)> TopDieDrop(
            Dictionary<string, VisionMarkResult> topVisionMarkResult,
            CancellationToken ct)
        {
            // ── 스테이지 공통 위치 1회 조회 (중복 제거) ─────────────────
            double placeCenterX = await GetPosition(MotionExtensions.H_X, "PLACE_CENTER", ct);
            double placeCenterY = await GetPosition(MotionExtensions.W_Y, "PLACE_CENTER", ct);

            // ── Hc 카메라 측정 결과 컨테이너 생성 ──────────────────────
            //   CameraType 반드시 설정: HC1 = 좌측 / HC2 = 우측 (W-Table 규칙)
            VisionMarkResult rightFid = new VisionMarkResult
            {
                CameraType = CameraType.HC2_HIGH,   // ★ W-Table 우측
                MarkType = MarkType.FIDUCIAL,
                DirectType = DirectType.RIGHT,
                StageX = placeCenterX,
                StageY = placeCenterY,
            };
            VisionMarkResult rightAlign = new VisionMarkResult
            {
                CameraType = CameraType.HC2_HIGH,   // ★
                MarkType = MarkType.ALIGN_MARK,
                DirectType = DirectType.RIGHT,
                StageX = placeCenterX,
                StageY = placeCenterY,
            };
            VisionMarkResult leftFid = new VisionMarkResult
            {
                CameraType = CameraType.HC1_HIGH,   // ★ W-Table 좌측
                MarkType = MarkType.FIDUCIAL,
                DirectType = DirectType.LEFT,
                StageX = placeCenterX,
                StageY = placeCenterY,
            };
            VisionMarkResult leftAlign = new VisionMarkResult
            {
                CameraType = CameraType.HC1_HIGH,   // ★
                MarkType = MarkType.ALIGN_MARK,
                DirectType = DirectType.LEFT,
                StageX = placeCenterX,
                StageY = placeCenterY,
            };

            // ── 레시피 파라미터 로드 (모두 double) ─────────────────────
            double topDieThickness = ParseRecipeDouble("TopDieThickness");
            double btmDieThickness = ParseRecipeDouble("BtmDieThickness");
            double centerToHC2OffsetX = ParseRecipeDouble("CenterToHC2OffsetX");
            double centerToHC2OffsetY = ParseRecipeDouble("CenterToHC2OffsetY");

            // ── W-Table로 이동 ──────────────────────────────────────────
            await Init_Head(ct);
            await MotionsMove([MotionExtensions.H_X, MotionExtensions.W_Y], "PLACE_CENTER", ct);
            await MotionsMove(MotionExtensions.H_Z, "PLACE_STANBY", -topDieThickness - btmDieThickness, ct);

            // ── 4점 측정 (HC1: 좌측 / HC2: 우측) ────────────────────────
            await communicationService.RequestAFStart(CameraType.HC1_HIGH, MarkType.FIDUCIAL, ct);
            var lFid = await communicationService.RequestVisionMarkPosition(
                MarkType.FIDUCIAL, CameraType.HC1_HIGH, DirectType.LEFT.ToString());
            if (lFid.Result == Result.NG) throw new Exception("Left Fiducial 측정 실패");
            leftFid.DxCamToMark = lFid.X;
            leftFid.DyCamToMark = lFid.Y;

            await communicationService.RequestAFStart(CameraType.HC1_HIGH, MarkType.ALIGN_MARK, ct);
            var lAlign = await communicationService.RequestVisionMarkPosition(
                MarkType.ALIGN_MARK, CameraType.HC1_HIGH, DirectType.LEFT.ToString());
            if (lAlign.Result == Result.NG) throw new Exception("Left Align 측정 실패");
            leftAlign.DxCamToMark = lAlign.X;
            leftAlign.DyCamToMark = lAlign.Y;

            await communicationService.RequestAFStart(CameraType.HC2_HIGH, MarkType.FIDUCIAL, ct);
            var rFid = await communicationService.RequestVisionMarkPosition(
                MarkType.FIDUCIAL, CameraType.HC2_HIGH, DirectType.RIGHT.ToString());
            if (rFid.Result == Result.NG) throw new Exception("Right Fiducial 측정 실패");
            rightFid.DxCamToMark = rFid.X;
            rightFid.DyCamToMark = rFid.Y;

            await communicationService.RequestAFStart(CameraType.HC2_HIGH, MarkType.ALIGN_MARK, ct);
            var rAlign = await communicationService.RequestVisionMarkPosition(
                MarkType.ALIGN_MARK, CameraType.HC2_HIGH, DirectType.RIGHT.ToString());
            if (rAlign.Result == Result.NG) throw new Exception("Right Align 측정 실패");
            rightAlign.DxCamToMark = rAlign.X;
            rightAlign.DyCamToMark = rAlign.Y;

            // ── P-Table (Top Die) 측정값 기반 각도/오프셋 ───────────────
            //   topVisionMarkResult 의 마크들은 CameraType = PC_HIGH 로 생성되어야 한다.
            var fidToDie = CalibrationService.FidToDie(
                topVisionMarkResult["RIGHT_FID"],
                topVisionMarkResult["RIGHT_ALIGN"]);

            double dTheta_die =
                CalibrationService.CalcTheta(topVisionMarkResult["RIGHT_FID"],
                                             topVisionMarkResult["LEFT_FID"])
              - CalibrationService.CalcTheta(topVisionMarkResult["RIGHT_ALIGN"],
                                             topVisionMarkResult["LEFT_ALIGN"]);

            // ── W-Table (Btm Die) 측정값 기반 각도/오프셋 ───────────────
            //   CenterToHC2OffsetX/Y 는 HC1↔HC2 기구 오프셋. 기존 WaferCalcTheta 가
            //   dx, dy 에 각각 더해 각도를 계산했던 로직을 여기서 동일하게 재현.
            var fidToWafer = CalibrationService.FidToWafer(rightFid, rightAlign);

            double dTheta_wafer =
                CalcThetaWithOffset(rightFid, leftFid, centerToHC2OffsetX, centerToHC2OffsetY)
              - CalcThetaWithOffset(rightAlign, leftAlign, centerToHC2OffsetX, centerToHC2OffsetY);

            // ── 최종 이동량 계산 + 실행 ────────────────────────────────
            var xyt = CalculateRelativeBondingMove(
                fidToDie.X, fidToDie.Y, dTheta_die,
                fidToWafer.X, fidToWafer.Y, dTheta_wafer);

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

            if (!result) throw new Exception("HeadPicker를 확인해주세요");
            return xyt;
        }

        /// <summary>
        /// HC1(좌)과 HC2(우) 카메라의 기구 오프셋(centerToHC2Offset)을 반영한 각도 계산.
        /// 기존 CalibrationService.WaferCalcTheta 를 이 파일 로컬로 이관.
        ///   dx = mark2.X - mark1.X + offsetX
        ///   dy = mark2.Y - mark1.Y + offsetY
        ///   θ  = atan2(dy, dx) * 180/π
        /// CenterY 는 CameraType 에 따라 자동 분기되므로 별도 CenterWaferY 호출 불필요.
        /// </summary>
        private static double CalcThetaWithOffset(
            VisionMarkResult mark1, VisionMarkResult mark2,
            double offsetX, double offsetY)
        {
            double dx = mark2.CenterX - mark1.CenterX + offsetX;
            double dy = mark2.CenterY - mark1.CenterY + offsetY;
            return Math.Atan2(dy, dx) * (180.0 / Math.PI);
        }

        /// <summary>
        /// 레시피 값을 double 로 파싱한다. 기존에 중복되던 try/throw 블록을 일괄 처리.
        /// </summary>
        private double ParseRecipeDouble(string name)
        {
            var raw = _recipeService.FindByParam(name)?.Value;
            if (!double.TryParse(raw, out double v))
                throw new Exception($"레시피 {name} 값이 Double 타입이 아닙니다 (입력={raw}).");
            return v;
        }

        // 가압 시퀀스
        public async Task Pressurize(int force = 2090, int delayMs = 1300)
        {
            var device = _deviceManager.GetDevice<PowerPmacDevice>("PMAC");
            string forceSet = $"P8276={force}";
            string command = "ENABLE PLC 11";
            await device.SendCommand(forceSet);
            await device.SendCommand(command);
            await Task.Delay(delayMs);
        }

        public (double moveX, double moveY, double moveTheta) CalculateRelativeBondingMove(
            double dx_fidToDie, double dy_fidToDie, double dTheta_die,     // P-table 데이터
            double dx_fidToWafer, double dy_fidToWafer, double theta_wafer // W-table 데이터
        )
        {
            // 주의: 변수명과 레시피 이름이 매칭되지 않음. 기존 코드 유지.
            //   _dx_cal ← ShankToHeadOffsetY
            //   _dy_cal ← ShankToHeadOffsetX
            double _dx_cal = ParseRecipeDouble("ShankToHeadOffsetY");
            double _dy_cal = ParseRecipeDouble("ShankToHeadOffsetX");

            // 1. 회전 중심 기준 통합 벡터 구성
            double dx_total = _dx_cal + dx_fidToDie;
            double dy_total = _dy_cal + dy_fidToDie;

            // 2. 최종 회전량 및 Shift 계산
            double finalTheta = theta_wafer + dTheta_die;
            double rad = finalTheta * (Math.PI / 180.0);
            double cosT = Math.Cos(rad);
            double sinT = Math.Sin(rad);

            double deltaX = dx_total * (cosT - 1) - dy_total * sinT;
            double deltaY = dx_total * sinT + dy_total * (cosT - 1);

            // 3. 보정 후 피디셜 대비 Die 의 상대 위치 계산
            double vDieAfterRotX = (dx_total - _dx_cal) + deltaX;
            double vDieAfterRotY = (dy_total - _dy_cal) + deltaY;

            // 4. 헤드 피디셜 위치에서 추가로 이동해야 할 상대 거리
            double finalMoveX = dx_fidToWafer - vDieAfterRotX;
            double finalMoveY = dy_fidToWafer - vDieAfterRotY;

            return (finalMoveX, finalMoveY, finalTheta);
        }
    }
}