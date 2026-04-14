using HCB.Data.Entity.Type;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static HCB.UI.SERVICE.CalibrationService;

namespace HCB.UI
{
    public partial class SequenceService : BackgroundService
    {
        public async Task MachineStartAsync(int topDie, int btmDie,  CancellationToken ct)
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

                // 1. Die wafer 얼라인
                var BtmDieAlign = await DTableCarrierAlign(btmDie, MarkType.DIE_CENTER_BOTTOM, ct);

                // 2. 픽업
                await DTableBTMPickup(btmDie, BtmDieAlign, ct);
                await BtmDieDrop(1, ct);
                await Init_Head(ct);

                // 3. 픽업 다이 얼라인
                var TopDieAlign = await DTableCarrierAlign(topDie, MarkType.DIE_CENTER_TOP, ct);
                await DTableTOPPickup(topDie, TopDieAlign, ct);

                // 4. 웨이퍼 다이 얼라인
                var topDieVisionResults = await TopDieVision(ct);

                // 5. 본딩
                await TopDieDrop(topDieVisionResults, ct);
                await Task.Delay(1000);
                await Init_Head(ct);
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
                var BtmDieAlign = await DTableCarrierAlign(dVac,MarkType.DIE_CENTER_BOTTOM, ct);
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
                var topDieAlign = await DTableCarrierAlign(dVac, MarkType.DIE_CENTER_TOP, ct);
                await DTableTOPPickup(dVac, topDieAlign, ct);
            }
            catch (Exception e)
            {
                _logger.Error(e.Message);
            }
        }


        public async Task<VisionMarkPositionResponse> TopLowAlign(
            int topDie, CancellationToken ct)
        {
            return await TopCarrierAlign(topDie, MarkType.DIE_CENTER_TOP, ct);
        }

        public async Task TopPickup(
            int topDie, VisionMarkPositionResponse visionResult, CancellationToken ct)
        {
            await DTableTOPPickup(topDie, visionResult, ct);
        }

        /// <summary>
        /// Top(Pc) 카메라로 4점 측정 후 캘리브레이션 보정 + Offset 계산.
        /// 결과는 ctx에 채워서 반환.
        /// </summary>
        public async Task<AlignContext> TopHighAlign(
            AlignContext ctx, CancellationToken ct)
        {
            ctx ??= new AlignContext();

            LoadCalibrationInto(ctx);

            ctx.TopRightFid = await TopDieVisionRightFid(ct);
            ctx.TopRightAlign = await TopDieVisionRightAlign(ct);
            ctx.TopLeftFid = await TopDieVisionLeftFid(ct);
            ctx.TopLeftAlign = await TopDieVisionLeftAlign(ct);

            ApplyTopPcCorrections(ctx);
            ComputeTopOffsets(ctx);

            return ctx;
        }

        /// <summary>
        /// TopDiePlace 후 Btm(Hc) 4점 측정 → 보정 → HcRO 변환 → Btm Offset 계산.
        /// TopHighAlign 이 먼저 실행되어 ctx.Top* 가 채워져 있어야 한다.
        /// </summary>
        public async Task<AlignContext> BtmHighAlign(
            AlignContext ctx, CancellationToken ct)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));

            LoadCalibrationInto(ctx);   // 재측정(파라미터 변경 대비)

            await TopDiePlace(ct);

            ctx.BtmRightFid = await BtmDieVisionRightFid(ct);
            ctx.BtmRightAlign = await BtmDieVisionRightAlign(ct);
            ctx.BtmLeftFid = await BtmDieVisionLeftFid(ct);
            ctx.BtmLeftAlign = await BtmDieVisionLeftAlign(ct);

            ApplyBtmCorrections(ctx);
            ComputeHcroCoords(ctx);
            ComputeBtmOffsets(ctx);

            return ctx;
        }

        /// <summary>
        /// 각도 보정 → 검증 → Shift → 검증 → 본딩.
        /// BtmHighAlign 이 먼저 실행되어 ctx.Hcro* 가 채워져 있어야 한다.
        /// </summary>
        public async Task TopPlace(
    AlignContext ctx, RecipeService recipeService, CancellationToken ct)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));

            var offsetX = double.Parse(_recipeService.FindByParam("X_ALIGN_OFFSET").Value);
            var offsetY = double.Parse(_recipeService.FindByParam("Y_ALIGN_OFFSET").Value);
            var offsetT = double.Parse(_recipeService.FindByParam("T_ALIGN_OFFSET").Value);
            // ── 입력값 전체 로깅 ─────────────────────────────────
            _logger.Information($"[INPUT] HcroLA    = ({ctx.HcroLA.X:F4}, {ctx.HcroLA.Y:F4})");
            _logger.Information($"[INPUT] HcroRA    = ({ctx.HcroRA.X:F4}, {ctx.HcroRA.Y:F4})");
            _logger.Information($"[INPUT] HcroLF    = ({ctx.HcroLF.X:F4}, {ctx.HcroLF.Y:F4})");
            _logger.Information($"[INPUT] HcroRF    = ({ctx.HcroRF.X:F4}, {ctx.HcroRF.Y:F4})");
            _logger.Information($"[INPUT] HcroTopLA = ({ctx.HcroTopLA.X:F4}, {ctx.HcroTopLA.Y:F4})");
            _logger.Information($"[INPUT] HcroTopRA = ({ctx.HcroTopRA.X:F4}, {ctx.HcroTopRA.Y:F4})");
            _logger.Information($"[INPUT] HcroTopLF = ({ctx.HcroTopLF.X:F4}, {ctx.HcroTopLF.Y:F4})");
            _logger.Information($"[INPUT] HcroTopRF = ({ctx.HcroTopRF.X:F4}, {ctx.HcroTopRF.Y:F4})");

            // ── B-Die 중심 ───────────────────────────────────────
            double btmCenterX = (ctx.HcroLA.X + ctx.HcroRA.X) / 2.0;
            double btmCenterY = (ctx.HcroLA.Y + ctx.HcroRA.Y) / 2.0;
            _logger.Information($"[BTM]   Center    = ({btmCenterX:F4}, {btmCenterY:F4})");

            // ── T-Die 중심 (회전 전) ─────────────────────────────
            double topCenterBeforeX = (ctx.HcroTopLA.X + ctx.HcroTopRA.X) / 2.0;
            double topCenterBeforeY = (ctx.HcroTopLA.Y + ctx.HcroTopRA.Y) / 2.0;
            _logger.Information($"[TOP]   Center(before rotate) = ({topCenterBeforeX:F4}, {topCenterBeforeY:F4})");

            // ── 얼라인 Angle 연산 ────────────────────────────────
            double thetaO = CalibrationMath.ComputeAlignAngle(
                ctx.HcroLA, ctx.HcroRA,
                ctx.HcroTopLA, ctx.HcroTopRA);
            double thetaS = ParseRecipe(recipeService, "SPEC_THETA") * Math.PI / 180.0;
            double specXs = ParseRecipe(recipeService, "SPEC_X");
            double specYs = ParseRecipe(recipeService, "SPEC_Y");
            double thetaF = thetaO - thetaS;

            _logger.Information($"[ANGLE] ThetaO = {thetaO:F6}rad ({thetaO * 180 / Math.PI:F4}deg)");
            _logger.Information($"[ANGLE] ThetaS = {thetaS:F6}rad ({thetaS * 180 / Math.PI:F4}deg)");
            _logger.Information($"[ANGLE] ThetaF = {thetaF:F6}rad ({thetaF * 180 / Math.PI:F4}deg)");
            _logger.Information($"[SPEC]  SpecXs = {specXs:F4}  SpecYs = {specYs:F4}");

            // ── hT 회전 ──────────────────────────────────────────
            await RelativeMotionsMove(MotionExtensions.H_T, thetaF, ct);

            // ── Shift 연산 ───────────────────────────────────────
            Point2D hcroTopLTPrime = CalibrationMath.RotateAroundPivot(
                ctx.HcroTopLA, Point2D.Zero, thetaF);
            Point2D hcroTopRTPrime = CalibrationMath.RotateAroundPivot(
                ctx.HcroTopRA, Point2D.Zero, thetaF);

            _logger.Information($"[ROT]   HcroTopLA → Prime = ({hcroTopLTPrime.X:F4}, {hcroTopLTPrime.Y:F4})");
            _logger.Information($"[ROT]   HcroTopRA → Prime = ({hcroTopRTPrime.X:F4}, {hcroTopRTPrime.Y:F4})");

            // T-Die 중심 (회전 후)
            double topCenterX = (hcroTopLTPrime.X + hcroTopRTPrime.X) / 2.0;
            double topCenterY = (hcroTopLTPrime.Y + hcroTopRTPrime.Y) / 2.0;
            _logger.Information($"[TOP]   Center(after rotate) = ({topCenterX:F4}, {topCenterY:F4})");

            // T-Die - B-Die 차이
            _logger.Information($"[DIFF]  X = {topCenterX - btmCenterX:F4}");
            _logger.Information($"[DIFF]  Y = {topCenterY - btmCenterY:F4}");

            double shiftX = topCenterX - btmCenterX + offsetX;
            double shiftY = topCenterY - btmCenterY + offsetY;

            _logger.Information($"[SHIFT] X = {shiftX:F4}  Y = {shiftY:F4}");

            await Task.WhenAll(
                RelativeMotionsMove(MotionExtensions.H_X, shiftX, ct),
                RelativeMotionsMove(MotionExtensions.W_Y, shiftY, ct));

            await Bonding(2000, ct);
            await Init_Head(ct);
        }
        // ═══════════════════════════════════════════════════
        //  Full Sequence (개별 단계 조합)
        // ═══════════════════════════════════════════════════

        public async Task<AlignContext> TopRunFullSequence(
            int topDie,
            VisionMarkPositionResponse visionTopLowAlign,
            RecipeService recipeService,
            CancellationToken ct)
        {
            var visionResult = await TopLowAlign(topDie, ct);
            await TopPickup(topDie, visionResult, ct);

            var ctx = await TopHighAlign(new AlignContext(), ct);
            ctx = await BtmHighAlign(ctx, ct);
            await TopPlace(ctx, recipeService, ct);

            return ctx;
        }

        // ═══════════════════════════════════════════════════
        //  private 헬퍼 (계산 로직)
        // ═══════════════════════════════════════════════════

        private void LoadCalibrationInto(AlignContext ctx)
        {
            var pcTParam = _paramService.FindByName(MotionExtensions.PC_T);
            var hc1Param = _paramService.FindByName(MotionExtensions.HC1_T);
            var hc2Param = _paramService.FindByName(MotionExtensions.HC2_T);
            var hcroXParam = _paramService.FindByName(MotionExtensions.HCRO_X);
            var hcroYParam = _paramService.FindByName(MotionExtensions.HCRO_Y);
            var hc1XParam = _paramService.FindByName(MotionExtensions.HC1_X);
            var hc1YParam = _paramService.FindByName(MotionExtensions.HC1_Y);
            var hc2XParam = _paramService.FindByName(MotionExtensions.HC2_X);
            var hc2YParam = _paramService.FindByName(MotionExtensions.HC2_Y);

            ctx.HasHcRO = hcroXParam.Id != 0 && hcroYParam.Id != 0
                       && hc1Param.Id != 0 && hc2Param.Id != 0
                       && hc1XParam.Id != 0 && hc1YParam.Id != 0
                       && hc2XParam.Id != 0 && hc2YParam.Id != 0;
            ctx.HasPcT = pcTParam.Id != 0;

            if (ctx.HasHcRO)
            {
                ctx.Hc1Rad = double.Parse(hc1Param.Value);
                ctx.Hc2Rad = double.Parse(hc2Param.Value);
                ctx.Hcro = Point2D.of(double.Parse(hcroXParam.Value), double.Parse(hcroYParam.Value));
                ctx.Hc1Offset = Point2D.of(double.Parse(hc1XParam.Value), double.Parse(hc1YParam.Value));
                ctx.Hc2Offset = Point2D.of(double.Parse(hc2XParam.Value), double.Parse(hc2YParam.Value));
            }
            if (ctx.HasPcT)
                ctx.PcTRad = double.Parse(pcTParam.Value);
        }

        private static void ApplyTopPcCorrections(AlignContext ctx)
        {
            if (!ctx.HasPcT) return;
            Rotate4Points(
                ctx.TopRightFid, ctx.TopRightAlign,
                ctx.TopLeftFid, ctx.TopLeftAlign,
                ctx.PcTRad, ctx.PcTRad);
        }

        private static void ApplyBtmCorrections(AlignContext ctx)
        {
            // 기구 오프셋
            ctx.BtmRightFid.StageX += ctx.Hc2Offset.X; ctx.BtmRightFid.StageY += ctx.Hc2Offset.Y;
            ctx.BtmRightAlign.StageX += ctx.Hc2Offset.X; ctx.BtmRightAlign.StageY += ctx.Hc2Offset.Y;
            ctx.BtmLeftFid.StageX += ctx.Hc1Offset.X; ctx.BtmLeftFid.StageY += ctx.Hc1Offset.Y;
            ctx.BtmLeftAlign.StageX += ctx.Hc1Offset.X; ctx.BtmLeftAlign.StageY += ctx.Hc1Offset.Y;

            if (!ctx.HasHcRO) return;
            Rotate4Points(
                ctx.BtmRightFid, ctx.BtmRightAlign,
                ctx.BtmLeftFid, ctx.BtmLeftAlign,
                ctx.Hc2Rad, ctx.Hc1Rad);
        }

        private static void ComputeTopOffsets(AlignContext ctx)
        {
            double rXFA = ctx.TopRightFid.CenterX - ctx.TopRightAlign.CenterX;
            double rYFA = ctx.TopRightFid.CenterY - ctx.TopRightAlign.CenterY;
            double lXFA = ctx.TopLeftFid.CenterX - ctx.TopLeftAlign.CenterX;
            double lYFA = ctx.TopLeftFid.CenterY - ctx.TopLeftAlign.CenterY;

            ctx.TopOffsetX = (ctx.TopRightFid.CenterX + ctx.TopLeftFid.CenterX) / 2.0
                           - (ctx.TopRightAlign.CenterX + ctx.TopLeftAlign.CenterX) / 2.0;
            ctx.TopOffsetY = (ctx.TopRightFid.CenterY + ctx.TopLeftFid.CenterY) / 2.0
                           - (ctx.TopRightAlign.CenterY + ctx.TopLeftAlign.CenterY) / 2.0;
            ctx.TopOffsetT = CalcTheta(ctx.TopLeftFid, ctx.TopRightFid)
                           - CalcTheta(ctx.TopLeftAlign, ctx.TopRightAlign);

            ctx.TopAlignRelOffsetX = -((rXFA + lXFA) / 2.0);
            ctx.TopAlignRelOffsetY = -((rYFA + lYFA) / 2.0);
            ctx.TopAlignRelOffsetT = ctx.TopOffsetT;
        }

        private static void ComputeHcroCoords(AlignContext ctx)
        {
            // ── B-Die (Hc) → HcRO 좌표 변환 ──────────────────────
            if (ctx.HasHcRO)
            {
                ctx.HcroLF = Point2D.of(
                    ctx.BtmLeftFid.CenterX - ctx.Hcro.X,
                    ctx.BtmLeftFid.CenterY - ctx.Hcro.Y);
                ctx.HcroLA = Point2D.of(
                    ctx.BtmLeftAlign.CenterX - ctx.Hcro.X,
                    ctx.BtmLeftAlign.CenterY - ctx.Hcro.Y);
                ctx.HcroRF = Point2D.of(
                    ctx.BtmRightFid.CenterX - ctx.Hcro.X,
                    ctx.BtmRightFid.CenterY - ctx.Hcro.Y);
                ctx.HcroRA = Point2D.of(
                    ctx.BtmRightAlign.CenterX - ctx.Hcro.X,
                    ctx.BtmRightAlign.CenterY - ctx.Hcro.Y);

                // ── T-Die → HcRO 좌표 변환 (PDF p.11-13) ─────────────

                // BH Fiducial Mark는 Hc·Pc 양쪽에서 보이는 동일 물리 마크
                ctx.HcroTopLF = ctx.HcroLF;
                ctx.HcroTopRF = ctx.HcroRF;

                // ── Step 1 (PDF p.12): Θ+ 계산 ───────────────────────
                // Pc는 하부 카메라(아래→위 촬영)이므로 X축 미러링 적용
                // Pc Fid 방향 벡터 (L→R, X 미러링)
                double pcFidDirX = -(ctx.TopRightFid.CenterX - ctx.TopLeftFid.CenterX);
                double pcFidDirY = ctx.TopRightFid.CenterY - ctx.TopLeftFid.CenterY;

                // HcRO Fid 방향 벡터 (L→R)
                double hcroFidDirX = ctx.HcroRF.X - ctx.HcroLF.X;
                double hcroFidDirY = ctx.HcroRF.Y - ctx.HcroLF.Y;

                // Θ+ = Pc Fid 벡터에서 HcRO Fid 벡터로의 회전각
                double thetaPlus = CalibrationMath.ComputeAlignAngle(
                    Point2D.Zero, Point2D.of(pcFidDirX, pcFidDirY),
                    Point2D.Zero, Point2D.of(hcroFidDirX, hcroFidDirY));

                // ── Step 2 (PDF p.13): T-Die Align Mark → HcRO ──────
                // 각 Fid→Align 상대 오프셋 (X 미러링) 을 Θ+로 회전

                // Left: LeftFid → LeftAlign
                double lXFA = -(ctx.TopLeftAlign.CenterX - ctx.TopLeftFid.CenterX);
                double lYFA = ctx.TopLeftAlign.CenterY - ctx.TopLeftFid.CenterY;
                var rotatedLeft = CalibrationMath.ApplyRotation(
                    Point2D.of(lXFA, lYFA), thetaPlus);
                ctx.HcroTopLA = Point2D.of(
                    ctx.HcroLF.X + rotatedLeft.X,
                    ctx.HcroLF.Y + rotatedLeft.Y);

                // Right: RightFid → RightAlign
                double rXFA = -(ctx.TopRightAlign.CenterX - ctx.TopRightFid.CenterX);
                double rYFA = ctx.TopRightAlign.CenterY - ctx.TopRightFid.CenterY;
                var rotatedRight = CalibrationMath.ApplyRotation(
                    Point2D.of(rXFA, rYFA), thetaPlus);
                ctx.HcroTopRA = Point2D.of(
                    ctx.HcroRF.X + rotatedRight.X,
                    ctx.HcroRF.Y + rotatedRight.Y);
            }
            else
            {
                ctx.HcroLF = Point2D.of(ctx.BtmLeftFid.CenterX, ctx.BtmLeftFid.CenterY);
                ctx.HcroLA = Point2D.of(ctx.BtmLeftAlign.CenterX, ctx.BtmLeftAlign.CenterY);
                ctx.HcroRF = Point2D.of(ctx.BtmRightFid.CenterX, ctx.BtmRightFid.CenterY);
                ctx.HcroRA = Point2D.of(ctx.BtmRightAlign.CenterX, ctx.BtmRightAlign.CenterY);
                ctx.HcroTopLF = Point2D.of(ctx.BtmLeftFid.CenterX, ctx.BtmLeftFid.CenterY);
                ctx.HcroTopRF = Point2D.of(ctx.BtmRightFid.CenterX, ctx.BtmRightFid.CenterY);
                ctx.HcroTopLA = Point2D.of(ctx.BtmLeftAlign.CenterX, ctx.BtmLeftAlign.CenterY);
                ctx.HcroTopRA = Point2D.of(ctx.BtmRightAlign.CenterX, ctx.BtmRightAlign.CenterY);
            }
        }

        //private static void ComputeHcroCoords(AlignContext ctx)
        //{
        //    // ── B-Die (Hc) → HcRO 좌표 변환 ──────────────────────
        //    if (ctx.HasHcRO)
        //    {
        //        ctx.HcroLF = Point2D.of(
        //            ctx.BtmLeftFid.CenterX - ctx.Hcro.X,
        //            ctx.BtmLeftFid.CenterY - ctx.Hcro.Y);
        //        ctx.HcroLA = Point2D.of(
        //            ctx.BtmLeftAlign.CenterX - ctx.Hcro.X,
        //            ctx.BtmLeftAlign.CenterY - ctx.Hcro.Y);
        //        ctx.HcroRF = Point2D.of(
        //            ctx.BtmRightFid.CenterX - ctx.Hcro.X,
        //            ctx.BtmRightFid.CenterY - ctx.Hcro.Y);
        //        ctx.HcroRA = Point2D.of(
        //            ctx.BtmRightAlign.CenterX - ctx.Hcro.X,
        //            ctx.BtmRightAlign.CenterY - ctx.Hcro.Y);

        //        // ── T-Die → HcRO 좌표 변환 ────────────────────────
        //        // Pc는 하부 카메라(아래→위 촬영)이므로 X축 미러링 적용
        //        ctx.HcroTopLF = ctx.HcroLF;
        //        ctx.HcroTopRF = ctx.HcroRF;

        //        // Left: LeftFid → LeftAlign 개별 오프셋 + X 미러링
        //        double lXFA = -(ctx.TopLeftAlign.CenterX - ctx.TopLeftFid.CenterX);
        //        double lYFA = ctx.TopLeftAlign.CenterY - ctx.TopLeftFid.CenterY;
        //        var rotatedLeft = CalibrationMath.ApplyRotation(
        //            Point2D.of(lXFA, lYFA), -ctx.TopAlignRelOffsetT);
        //        ctx.HcroTopLA = Point2D.of(
        //            ctx.HcroLF.X + rotatedLeft.X,
        //            ctx.HcroLF.Y + rotatedLeft.Y);

        //        // Right: RightFid → RightAlign 개별 오프셋 + X 미러링
        //        double rXFA = -(ctx.TopRightAlign.CenterX - ctx.TopRightFid.CenterX);
        //        double rYFA = ctx.TopRightAlign.CenterY - ctx.TopRightFid.CenterY;
        //        var rotatedRight = CalibrationMath.ApplyRotation(
        //            Point2D.of(rXFA, rYFA), -ctx.TopAlignRelOffsetT);
        //        ctx.HcroTopRA = Point2D.of(
        //            ctx.HcroRF.X + rotatedRight.X,
        //            ctx.HcroRF.Y + rotatedRight.Y);
        //    }
        //    else
        //    {
        //        ctx.HcroLF = Point2D.of(ctx.BtmLeftFid.CenterX, ctx.BtmLeftFid.CenterY);
        //        ctx.HcroLA = Point2D.of(ctx.BtmLeftAlign.CenterX, ctx.BtmLeftAlign.CenterY);
        //        ctx.HcroRF = Point2D.of(ctx.BtmRightFid.CenterX, ctx.BtmRightFid.CenterY);
        //        ctx.HcroRA = Point2D.of(ctx.BtmRightAlign.CenterX, ctx.BtmRightAlign.CenterY);
        //        ctx.HcroTopLF = Point2D.of(ctx.BtmLeftFid.CenterX, ctx.BtmLeftFid.CenterY);
        //        ctx.HcroTopRF = Point2D.of(ctx.BtmRightFid.CenterX, ctx.BtmRightFid.CenterY);
        //        ctx.HcroTopLA = Point2D.of(ctx.BtmLeftAlign.CenterX, ctx.BtmLeftAlign.CenterY);
        //        ctx.HcroTopRA = Point2D.of(ctx.BtmRightAlign.CenterX, ctx.BtmRightAlign.CenterY);
        //    }
        //}
        private static void ComputeBtmOffsets(AlignContext ctx)
        {
            double btmRXFA = ctx.BtmRightFid.CenterX - ctx.BtmRightAlign.CenterX;
            double btmRYFA = ctx.BtmRightFid.CenterY - ctx.BtmRightAlign.CenterY;
            double btmLXFA = ctx.BtmLeftFid.CenterX - ctx.BtmLeftAlign.CenterX;
            double btmLYFA = ctx.BtmLeftFid.CenterY - ctx.BtmLeftAlign.CenterY;

            double topInBtmX = -ctx.TopAlignRelOffsetX;
            double topInBtmY = ctx.TopAlignRelOffsetY;
            double topInBtmT = -ctx.TopAlignRelOffsetT;

            ctx.BtmOffsetX = topInBtmX - (btmRXFA + btmLXFA) / 2.0;
            ctx.BtmOffsetY = topInBtmY - (btmRYFA + btmLYFA) / 2.0;
            ctx.BtmOffsetT = topInBtmT - (CalcTheta(ctx.BtmLeftFid, ctx.BtmRightFid)
                                        - CalcTheta(ctx.BtmLeftAlign, ctx.BtmRightAlign));
        }

        private async Task VerifyAfterRotation(AlignContext ctx, double thetaF, CancellationToken ct)
        {
            var vRF = await BtmDieVisionRightFid(ct);
            var vLF = await BtmDieVisionLeftFid(ct);

            vRF.StageX -= ctx.Hc2Offset.X;
            vRF.StageY -= ctx.Hc2Offset.Y;
            vLF.StageX -= ctx.Hc1Offset.X;
            vLF.StageY -= ctx.Hc1Offset.Y;

            if (!ctx.HasHcRO) return;

            var cRF = CalibrationMath.ApplyRotation(
                Point2D.of(vRF.DxCamToMark, vRF.DyCamToMark), ctx.Hc2Rad);
            var cLF = CalibrationMath.ApplyRotation(
                Point2D.of(vLF.DxCamToMark, vLF.DyCamToMark), ctx.Hc1Rad);

            vRF.DxCamToMark = cRF.X; vRF.DyCamToMark = cRF.Y;
            vLF.DxCamToMark = cLF.X; vLF.DyCamToMark = cLF.Y;

            var vHcroRF = Point2D.of(vRF.CenterX - ctx.Hcro.X, vRF.CenterY - ctx.Hcro.Y);
            var vHcroLF = Point2D.of(vLF.CenterX - ctx.Hcro.X, vLF.CenterY - ctx.Hcro.Y);

            // 회전 후 HcRO 원점으로부터의 거리는 변하지 않아야 함
            double dR = Math.Abs(
                CalibrationMath.Distance(Point2D.Zero, ctx.HcroRF) -
                CalibrationMath.Distance(Point2D.Zero, vHcroRF));
            double dL = Math.Abs(
                CalibrationMath.Distance(Point2D.Zero, ctx.HcroLF) -
                CalibrationMath.Distance(Point2D.Zero, vHcroLF));

            if (dR > 2.0 || dL > 2.0)
                _logger.Warning(
                    $"HcRO 회전 중심 검증 실패! R={dR:F3}µm  L={dL:F3}µm");
            else
                _logger.Information("HcRO 회전 중심 검증 통과");
        }

        // ───────────────────────────────────────────────────────────
        // VerifyAfterShift
        // ───────────────────────────────────────────────────────────
        private async Task VerifyAfterShift(
            AlignContext ctx, double totalShiftX, double totalShiftY, CancellationToken ct)
        {
            var vRA = await BtmDieVisionRightAlign(ct);
            var vLA = await BtmDieVisionLeftAlign(ct);

            vRA.StageX -= ctx.Hc2Offset.X; vRA.StageY -= ctx.Hc2Offset.Y;
            vLA.StageX -= ctx.Hc1Offset.X; vLA.StageY -= ctx.Hc1Offset.Y;

            if (!ctx.HasHcRO) return;

            var cRA = CalibrationMath.ApplyRotation(
                Point2D.of(vRA.DxCamToMark, vRA.DyCamToMark), ctx.Hc2Rad);
            var cLA = CalibrationMath.ApplyRotation(
                Point2D.of(vLA.DxCamToMark, vLA.DyCamToMark), ctx.Hc1Rad);

            vRA.DxCamToMark = cRA.X; vRA.DyCamToMark = cRA.Y;
            vLA.DxCamToMark = cLA.X; vLA.DyCamToMark = cLA.Y;

            var vHcroRA = Point2D.of(vRA.CenterX - ctx.Hcro.X, vRA.CenterY - ctx.Hcro.Y);
            var vHcroLA = Point2D.of(vLA.CenterX - ctx.Hcro.X, vLA.CenterY - ctx.Hcro.Y);

            // Stage가 +totalShift 만큼 이동했으므로
            // HcRO 기준 Mark 좌표는 -totalShift 방향으로 이동
            var expLA = Point2D.of(ctx.HcroLA.X - totalShiftX, ctx.HcroLA.Y - totalShiftY);
            var expRA = Point2D.of(ctx.HcroRA.X - totalShiftX, ctx.HcroRA.Y - totalShiftY);

            bool lOk = CalibrationMath.VerifyPositionStability(expLA, vHcroLA, 2.0);
            bool rOk = CalibrationMath.VerifyPositionStability(expRA, vHcroRA, 2.0);

            if (!lOk || !rOk)
                _logger.Warning(
                    $"Shift 검증 실패! " +
                    $"L={CalibrationMath.Distance(expLA, vHcroLA):F3}µm  " +
                    $"R={CalibrationMath.Distance(expRA, vHcroRA):F3}µm");
            else
                _logger.Information("Shift 위치 검증 통과");
        }

        // ── 중복 제거 유틸 ───────────────────────────────────────
        private static void Rotate4Points(
            VisionMarkResult rightFid, VisionMarkResult rightAlign,
            VisionMarkResult leftFid, VisionMarkResult leftAlign,
            double rightRad, double leftRad)
        {
            Apply(rightFid, rightRad);
            Apply(rightAlign, rightRad);
            Apply(leftFid, leftRad);
            Apply(leftAlign, leftRad);

            static void Apply(VisionMarkResult m, double rad)
            {
                var p = CalibrationMath.ApplyRotation(Point2D.of(m.DxCamToMark, m.DyCamToMark), rad);
                m.DxCamToMark = p.X; m.DyCamToMark = p.Y;
            }
        }

        private static double ParseRecipe(RecipeService svc, string paramName)
        {
            var p = svc.FindByParam(paramName);
            return p != null ? double.Parse(p.Value) : 0.0;
        }
    }

}
