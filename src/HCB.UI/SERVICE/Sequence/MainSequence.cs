using HCB.Data.Entity.Type;
using Microsoft.Extensions.Hosting;
using SharpDX.Direct2D1.Effects;
using SharpDX.Direct3D9;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using static HCB.UI.SERVICE.CalibrationService;

namespace HCB.UI
{
    public partial class SequenceService : BackgroundService
    {
        // ═══════════════════════════════════════════════════
        //  Public Sequence Entry Points
        // ═══════════════════════════════════════════════════

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
                var BtmDieAlign = await DTableCarrierAlign(dVac, MarkType.DIE_CENTER_BOTTOM, ct);
                await DTableBTMPickup(dVac, BtmDieAlign, ct);
            }
            catch (Exception e)
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

        // ═══════════════════════════════════════════════════
        //  Step 1: Top(Pc) 카메라 4점 측정
        //   비전 원본 → Raw 필드 (절대 수정 금지)
        //   보정 결과 → Corrected 필드
        // ═══════════════════════════════════════════════════

        public async Task<AlignContext> TopHighAlign(
            AlignContext ctx, CancellationToken ct)
        {
            ctx ??= new AlignContext();            
            LoadCalibrationInto(ctx);

            ctx.TopRightFidRaw = await TopDieVisionRightFid(ct);
            ctx.TopRightAlignRaw = await TopDieVisionRightAlign(ct);
            ctx.TopLeftFidRaw = await TopDieVisionLeftFid(ct);
            ctx.TopLeftAlignRaw = await TopDieVisionLeftAlign(ct);

            // Raw → Clone → 보정 → Corrected
            ApplyTopPcCorrections(ctx);

            // Corrected 기반 오프셋 계산
            ComputeTopOffsets(ctx);

            return ctx;
        }

        // ═══════════════════════════════════════════════════
        //  Step 2: Btm(Hc) 4점 측정 → 보정 → HcRO 변환
        //   TopHighAlign 완료 후 ctx.Top*Corrected 필요
        // ═══════════════════════════════════════════════════

        public async Task<AlignContext> BtmHighAlign(
            AlignContext ctx, CancellationToken ct)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));

            await TopDiePlace(ct);

            // 비전 원본 → Raw (이후 어떤 메서드도 수정 금지)
            ctx.BtmRightFidRaw = await BtmDieVisionRightFid(ct);
            ctx.BtmRightAlignRaw = await BtmDieVisionRightAlign(ct);
            ctx.BtmLeftFidRaw = await BtmDieVisionLeftFid(ct);
            ctx.BtmLeftAlignRaw = await BtmDieVisionLeftAlign(ct);
            //await GetHcro(ctx, ct);
            LoadCalibrationInto(ctx);   

            // 카메라 오프셋 + 회전 보정
            ApplyBtmCorrections(ctx);
            // 회전 중심 좌표로 변환
            ComputeHcroCoords(ctx);
            ComputeBtmOffsets(ctx);

            return ctx;
        }

        public async Task GetHcro(AlignContext ctx, CancellationToken ct)
        {
            try
            {
                var hc1_x = ParseDouble(_paramService.FindByName("HC1_X").Value);
                var hc1_y = ParseDouble(_paramService.FindByName("HC1_Y").Value);
                var hc2_x = ParseDouble(_paramService.FindByName("HC2_X").Value);
                var hc2_y = ParseDouble(_paramService.FindByName("HC2_Y").Value);

                var hc1_0 = await VisionResult(CameraType.HC1_HIGH, MarkType.FIDUCIAL, DirectType.LEFT, MotionExtensions.W_Y, ct);
                var hc2_0 = await VisionResult(CameraType.HC2_HIGH, MarkType.FIDUCIAL, DirectType.RIGHT, MotionExtensions.W_Y, ct);

                await MotionsMove(MotionExtensions.H_T, 1.5, ct);

                var hc1_15 = await VisionResult(CameraType.HC1_HIGH, MarkType.FIDUCIAL, DirectType.LEFT, MotionExtensions.W_Y, ct);
                var hc2_15 = await VisionResult(CameraType.HC2_HIGH, MarkType.FIDUCIAL, DirectType.RIGHT, MotionExtensions.W_Y, ct);

                _logger.Information($"HC1_param=({hc1_x},{hc1_y}), HC2_param=({hc2_x},{hc2_y})");
                _logger.Information($"hc1_0=({hc1_0.CenterX},{hc1_0.CenterY})");
                _logger.Information($"hc1_15=({hc1_15.CenterX},{hc1_15.CenterY})");
                _logger.Information($"hc2_0=({hc2_0.CenterX},{hc2_0.CenterY})");
                _logger.Information($"hc2_15=({hc2_15.CenterX},{hc2_15.CenterY})");

                // ── 회전 중심 계산 ────────────────────────────────────
                var hcRO = CalibrationMath.ComputeHcRO2(
                    Point2D.of(hc1_0.CenterX + hc1_x, hc1_0.CenterY + hc1_y),
                    Point2D.of(hc1_15.CenterX + hc1_x, hc1_15.CenterY + hc1_y),
                    Point2D.of(hc2_0.CenterX + hc2_x, hc2_0.CenterY + hc2_y),
                    Point2D.of(hc2_15.CenterX + hc2_x, hc2_15.CenterY + hc2_y));
                      
                ECParamDto dto = _paramService.FindByName(MotionExtensions.HCRO_X);
                dto.Value = hcRO.X.ToString();
                dto.ValueType = Data.Entity.Type.ValueType.Double;
                if (dto.Id == 0) { await _paramService.AddParam(dto); }
                else { await _paramService.UpdateParam(dto); }

                ECParamDto dto2 = _paramService.FindByName(MotionExtensions.HCRO_Y);
                dto2.Value = hcRO.Y.ToString();
                dto2.ValueType = Data.Entity.Type.ValueType.Double;
                if (dto2.Id == 0) { await _paramService.AddParam(dto2); }
                else { await _paramService.UpdateParam(dto2); }
                await MotionsMove(MotionExtensions.H_T, 0, ct);
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { _logger.Error(e, "CreateHcRo failed"); }
        }

        // ═══════════════════════════════════════════════════
        //  Step 3: 각도 보정 → Shift → 본딩
        //   BtmHighAlign 완료 후 ctx.Hcro* 필요
        // ═══════════════════════════════════════════════════

        public async Task TopPlace(
            AlignContext ctx, RecipeService recipeService, ObservableCollection<BondingDataPoint> bondingDataPoints, CancellationToken ct)
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
                ctx.HcroTopLA, ctx.HcroTopRA);      // rad 
            double thetaS = ParseRecipe(recipeService, "SPEC_THETA") * Math.PI / 180.0;     //rad 
            double specXs = ParseRecipe(recipeService, "SPEC_X");
            double specYs = ParseRecipe(recipeService, "SPEC_Y");
            double thetaF = thetaO - thetaS + offsetT * Math.PI / 180.0;

            _logger.Information($"[ANGLE] ThetaO = {thetaO:F6}rad ({thetaO * 180 / Math.PI:F4}deg)");
            _logger.Information($"[ANGLE] ThetaS = {thetaS:F6}rad ({thetaS * 180 / Math.PI:F4}deg)");
            _logger.Information($"[ANGLE] ThetaF = {thetaF:F6}rad ({thetaF * 180 / Math.PI:F4}deg)");
            _logger.Information($"[SPEC]  SpecXs = {specXs:F4}  SpecYs = {specYs:F4}");

            // ── hT 회전 ──────────────────────────────────────────
            //await RelativeMotionsMove(MotionExtensions.H_T, thetaF * 180.0 / Math.PI, ct);

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

            //await Task.WhenAll(
            //    RelativeMotionsMove(MotionExtensions.H_X, shiftX, ct),
            //    RelativeMotionsMove(MotionExtensions.W_Y, shiftY, ct));

            //await RelativeMotionsMove(MotionExtensions.W_Y, shiftY, ct);

            ctx.FinalThetaO = thetaO;
            ctx.FinalThetaF = thetaF;
            ctx.FinalShiftX = shiftX;
            ctx.FinalShiftY = shiftY;
            ctx.OffsetXApplied = offsetX;
            ctx.OffsetYApplied = offsetY;
            ctx.OffsetTApplied = offsetT;

            await Bonding(bondingDataPoints, ct);
            await Init_Head(ct);
            await Task.Delay(2000);
            await MotionsMove("H_T", 0, ct);
        }

        // ═══════════════════════════════════════════════════
        //  Full Sequence (개별 단계 조합)
        //  ★ ctx 는 반드시 새 인스턴스 생성
        // ═══════════════════════════════════════════════════

        //public async Task<AlignContext> TopRunFullSequence(
        //    int topDie,
        //    VisionMarkPositionResponse visionTopLowAlign,
        //    RecipeService recipeService,
        //    ObservableCollection<BondingDataPoint> bondingDataPoints,
        //    CancellationToken ct)
        //{
        //    var visionResult = await TopLowAlign(topDie, ct);
        //    await TopPickup(topDie, visionResult, ct);

        //    var ctx = await TopHighAlign(new AlignContext(), ct);
        //    ctx = await BtmHighAlign(ctx, ct);
        //    await TopPlace(ctx, recipeService, bondingDataPoints, ct);

        //    return ctx;
        //}

        // ═══════════════════════════════════════════════════
        //  private: 캘리브레이션 파라미터 로드
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
                ctx.Hc1Rad = ParseDouble(hc1Param.Value) * Math.PI / 180.0;
                ctx.Hc2Rad = ParseDouble(hc2Param.Value) * Math.PI / 180.0;
                ctx.Hcro = Point2D.of(ParseDouble(hcroXParam.Value), ParseDouble(hcroYParam.Value));
                ctx.Hc1Offset = Point2D.of(ParseDouble(hc1XParam.Value), ParseDouble(hc1YParam.Value));
                ctx.Hc2Offset = Point2D.of(ParseDouble(hc2XParam.Value), ParseDouble(hc2YParam.Value));
            }
            if (ctx.HasPcT)
                ctx.PcTRad = ParseDouble(pcTParam.Value) * Math.PI / 180.0;
        }

        // ═══════════════════════════════════════════════════
        //  private: Top(Pc) 보정
        //   Raw → Clone → 회전 보정 → Corrected
        //   ★ Raw 읽기만, 쓰기는 Corrected 에만
        // ═══════════════════════════════════════════════════

        private static void ApplyTopPcCorrections(AlignContext ctx)
        {
            if (!ctx.HasPcT)
            {
                // 보정 없이 Raw 복사본을 Corrected 에 저장
                ctx.TopRightFidCorrected = ctx.TopRightFidRaw.Clone();
                ctx.TopRightAlignCorrected = ctx.TopRightAlignRaw.Clone();
                ctx.TopLeftFidCorrected = ctx.TopLeftFidRaw.Clone();
                ctx.TopLeftAlignCorrected = ctx.TopLeftAlignRaw.Clone();
                return;
            }

            // Clone → 회전 보정 → Corrected
            ctx.TopRightFidCorrected = ApplyRotationToCopy(ctx.TopRightFidRaw, ctx.PcTRad);
            ctx.TopRightAlignCorrected = ApplyRotationToCopy(ctx.TopRightAlignRaw, ctx.PcTRad);
            ctx.TopLeftFidCorrected = ApplyRotationToCopy(ctx.TopLeftFidRaw, ctx.PcTRad);
            ctx.TopLeftAlignCorrected = ApplyRotationToCopy(ctx.TopLeftAlignRaw, ctx.PcTRad);
        }

        // ═══════════════════════════════════════════════════
        //  private: Btm(Hc) 보정
        //   Raw → Clone → 기구 오프셋 → Hc 회전 → Corrected
        //   ★ Raw 읽기만, 쓰기는 Corrected 에만
        // ═══════════════════════════════════════════════════

        private static void ApplyBtmCorrections(AlignContext ctx)
        {
            // ── Raw → Clone (원본 보존) ──────────────────────
            var rightFid = ctx.BtmRightFidRaw.Clone();
            var rightAlign = ctx.BtmRightAlignRaw.Clone();
            var leftFid = ctx.BtmLeftFidRaw.Clone();
            var leftAlign = ctx.BtmLeftAlignRaw.Clone();

            // ── 기구 오프셋 적용 (Clone 에만) ────────────────
            rightFid.StageX += ctx.Hc2Offset.X; rightFid.StageY += ctx.Hc2Offset.Y;
            rightAlign.StageX += ctx.Hc2Offset.X; rightAlign.StageY += ctx.Hc2Offset.Y;
            leftFid.StageX += ctx.Hc1Offset.X; leftFid.StageY += ctx.Hc1Offset.Y;
            leftAlign.StageX += ctx.Hc1Offset.X; leftAlign.StageY += ctx.Hc1Offset.Y;

            // ── Hc 카메라 회전 보정 ──────────────────────────
            if (ctx.HasHcRO)
            {
                rightFid = ApplyRotationToCopy(rightFid, ctx.Hc2Rad);
                rightAlign = ApplyRotationToCopy(rightAlign, ctx.Hc2Rad);
                leftFid = ApplyRotationToCopy(leftFid, ctx.Hc1Rad);
                leftAlign = ApplyRotationToCopy(leftAlign, ctx.Hc1Rad);
            }

            // ── Corrected 저장 ───────────────────────────────
            ctx.BtmRightFidCorrected = rightFid;
            ctx.BtmRightAlignCorrected = rightAlign;
            ctx.BtmLeftFidCorrected = leftFid;
            ctx.BtmLeftAlignCorrected = leftAlign;
        }

        // ═══════════════════════════════════════════════════
        //  private: Top 오프셋 계산
        //   Corrected 읽기만 (수정 없음)
        // ═══════════════════════════════════════════════════

        private static void ComputeTopOffsets(AlignContext ctx)
        {
            var tRF = ctx.TopRightFidCorrected;
            var tRA = ctx.TopRightAlignCorrected;
            var tLF = ctx.TopLeftFidCorrected;
            var tLA = ctx.TopLeftAlignCorrected;

            double rXFA = tRF.CenterX - tRA.CenterX;
            double rYFA = tRF.CenterY - tRA.CenterY;
            double lXFA = tLF.CenterX - tLA.CenterX;
            double lYFA = tLF.CenterY - tLA.CenterY;

            ctx.TopOffsetX = (tRF.CenterX + tLF.CenterX) / 2.0
                           - (tRA.CenterX + tLA.CenterX) / 2.0;
            ctx.TopOffsetY = (tRF.CenterY + tLF.CenterY) / 2.0
                           - (tRA.CenterY + tLA.CenterY) / 2.0;
            ctx.TopOffsetT = CalcTheta(tLF, tRF) - CalcTheta(tLA, tRA);

            ctx.TopAlignRelOffsetX = -((rXFA + lXFA) / 2.0);
            ctx.TopAlignRelOffsetY = -((rYFA + lYFA) / 2.0);
            ctx.TopAlignRelOffsetT = ctx.TopOffsetT;
        }

        // ═══════════════════════════════════════════════════
        //  private: HcRO 좌표 변환
        //   Corrected 읽기만 (수정 없음)
        //
        //  변환 순서:
        //   ① Btm(Hc) → HcRO 좌표 (평행 이동)
        //   ② T-Die Fid = B-Die Fid (deep copy)
        //   ③ Pc Fid/Align 오프셋 X·Y 미러링
        //   ④ 비등방 배율 Sx·Sy 산출
        //   ⑤ 배율 먼저 → 회전 보정 (commutative 아님)
        //   ⑥ HcRO Fid 좌표에 더해 HcroTopLA·RA 산출
        // ═══════════════════════════════════════════════════

        private static void ComputeHcroCoords(AlignContext ctx)
        {
            // Corrected 결과 참조 (Raw 접근 금지)
            var bRF = ctx.BtmRightFidCorrected;
            var bRA = ctx.BtmRightAlignCorrected;
            var bLF = ctx.BtmLeftFidCorrected;
            var bLA = ctx.BtmLeftAlignCorrected;

            var tRF = ctx.TopRightFidCorrected;
            var tRA = ctx.TopRightAlignCorrected;
            var tLF = ctx.TopLeftFidCorrected;
            var tLA = ctx.TopLeftAlignCorrected;

            if (ctx.HasHcRO)
            {
                // ── ① B-Die (Hc) -> HcRO 좌표 변환 ──────────────
                ctx.HcroLF = Point2D.of(bLF.CenterX - ctx.Hcro.X, bLF.CenterY - ctx.Hcro.Y);
                ctx.HcroLA = Point2D.of(bLA.CenterX - ctx.Hcro.X, bLA.CenterY - ctx.Hcro.Y);
                ctx.HcroRF = Point2D.of(bRF.CenterX - ctx.Hcro.X, bRF.CenterY - ctx.Hcro.Y);
                ctx.HcroRA = Point2D.of(bRA.CenterX - ctx.Hcro.X, bRA.CenterY - ctx.Hcro.Y);

                // ── ② T-Die Fid = B-Die Fid (기준점 설정) ─────────
                ctx.HcroTopLF = Point2D.of(ctx.HcroLF.X, ctx.HcroLF.Y);
                ctx.HcroTopRF = Point2D.of(ctx.HcroRF.X, ctx.HcroRF.Y);

                // ── ③ Pc 공간 벡터 (미러링 핵심 수정) ─────────────────
                // P-Camera가 위를 보고 T-Die의 '하단'을 보므로, 
                // 설계상의 Right(오른쪽)가 비전상에서는 Left(왼쪽)로 보일 수 있습니다.

                double pcFidDx = (tRF.CenterX - tLF.CenterX) ; 
                double pcFidDy = (tRF.CenterY - tLF.CenterY) * 1.0;  // Y 유지 (혹은 장비 특성에 따라 -1)

                double hcroFidDx = ctx.HcroRF.X - ctx.HcroLF.X;
                double hcroFidDy = ctx.HcroRF.Y - ctx.HcroLF.Y;

                // ── ④ 스케일 및 변환 각도(Theta Plus) 계산 ─────────────
                double pcFidDist = Math.Sqrt(pcFidDx * pcFidDx + pcFidDy * pcFidDy);
                double hcroFidDist = Math.Sqrt(hcroFidDx * hcroFidDx + hcroFidDy * hcroFidDy);
                double scale = (pcFidDist > 1e-6) ? hcroFidDist / pcFidDist : 1.0;
                //double scale = 1;
                // ThetaPlus: Pc 벡터를 HcRO 벡터 방향으로 일치시키기 위한 회전량
                double thetaPlus = Math.Atan2(hcroFidDy, hcroFidDx) - Math.Atan2(pcFidDy, pcFidDx);

                // ── ⑤ T-Die Align -> HcRO 좌표 변환 ─────────────
                // Fid에서 Align Mark로 가는 '상대 벡터'를 구한 뒤, 
                // 미러링 -> 스케일 -> 회전(ThetaPlus) 순으로 적용합니다.

                // [Left Align 변환]
                double lVecX = (tLA.CenterX - tLF.CenterX) * 1.0; 
                double lVecY = (tLA.CenterY - tLF.CenterY) * 1.0;  // Y 유지
                var rotL = CalibrationMath.ApplyRotation(Point2D.of(lVecX * scale, lVecY * scale), thetaPlus);
                ctx.HcroTopLA = Point2D.of(ctx.HcroLF.X + rotL.X, ctx.HcroLF.Y + rotL.Y);

                // [Right Align 변환]
                double rVecX = (tRA.CenterX - tRF.CenterX) * 1.0; 
                double rVecY = (tRA.CenterY - tRF.CenterY) * 1.0;  // Y 유지
                var rotR = CalibrationMath.ApplyRotation(Point2D.of(rVecX * scale, rVecY * scale), thetaPlus);
                ctx.HcroTopRA = Point2D.of(ctx.HcroRF.X + rotR.X, ctx.HcroRF.Y + rotR.Y);

                // 진단값 기록
                ctx.PcHcroScaleX = scale;
                ctx.PcHcroScaleY = scale;
                ctx.PcHcroThetaPlus = thetaPlus;
            }
            else
            {
                // HcRO 캘리브레이션 없음 → 절대좌표 그대로
                ctx.HcroLF = Point2D.of(bLF.CenterX, bLF.CenterY);
                ctx.HcroLA = Point2D.of(bLA.CenterX, bLA.CenterY);
                ctx.HcroRF = Point2D.of(bRF.CenterX, bRF.CenterY);
                ctx.HcroRA = Point2D.of(bRA.CenterX, bRA.CenterY);

                ctx.HcroTopLF = Point2D.of(bLF.CenterX, bLF.CenterY);
                ctx.HcroTopRF = Point2D.of(bRF.CenterX, bRF.CenterY);
                ctx.HcroTopLA = Point2D.of(bLA.CenterX, bLA.CenterY);
                ctx.HcroTopRA = Point2D.of(bRA.CenterX, bRA.CenterY);

                ctx.PcHcroScaleX = 1.0;
                ctx.PcHcroScaleY = 1.0;
                ctx.PcHcroThetaPlus = 0.0;
                ctx.ScaleFallbackApplied = false;
            }
        }
        // ═══════════════════════════════════════════════════
        //  private: Btm 오프셋 계산
        //   Corrected 읽기만 (수정 없음)
        // ═══════════════════════════════════════════════════

        private static void ComputeBtmOffsets(AlignContext ctx)
        {
            var bRF = ctx.BtmRightFidCorrected;
            var bRA = ctx.BtmRightAlignCorrected;
            var bLF = ctx.BtmLeftFidCorrected;
            var bLA = ctx.BtmLeftAlignCorrected;

            double btmRXFA = bRF.CenterX - bRA.CenterX;
            double btmRYFA = bRF.CenterY - bRA.CenterY;
            double btmLXFA = bLF.CenterX - bLA.CenterX;
            double btmLYFA = bLF.CenterY - bLA.CenterY;

            double topInBtmX = -ctx.TopAlignRelOffsetX;
            double topInBtmY = ctx.TopAlignRelOffsetY;
            double topInBtmT = -ctx.TopAlignRelOffsetT;

            ctx.BtmOffsetX = topInBtmX - (btmRXFA + btmLXFA) / 2.0;
            ctx.BtmOffsetY = topInBtmY - (btmRYFA + btmLYFA) / 2.0;
            ctx.BtmOffsetT = topInBtmT - (CalcTheta(bLF, bRF) - CalcTheta(bLA, bRA));
        }

        // ═══════════════════════════════════════════════════
        //  Verify: 회전 후 HcRO 중심 검증
        //   신규 비전 → Clone → 로컬 처리 → ctx 오염 없음
        // ═══════════════════════════════════════════════════

        private async Task VerifyAfterRotation(AlignContext ctx, double thetaF, CancellationToken ct)
        {
            // 검증용 신규 비전 측정 (ctx 와 독립)
            var rawRF = await BtmDieVisionRightFid(ct);
            var rawLF = await BtmDieVisionLeftFid(ct);

            if (!ctx.HasHcRO) return;

            // Clone → 기구 오프셋 → 회전 보정 (로컬에서만)
            var vRF = rawRF.Clone();
            var vLF = rawLF.Clone();
            vRF.StageX -= ctx.Hc2Offset.X; vRF.StageY -= ctx.Hc2Offset.Y;
            vLF.StageX -= ctx.Hc1Offset.X; vLF.StageY -= ctx.Hc1Offset.Y;

            vRF = ApplyRotationToCopy(vRF, ctx.Hc2Rad);
            vLF = ApplyRotationToCopy(vLF, ctx.Hc1Rad);

            var vHcroRF = Point2D.of(vRF.CenterX - ctx.Hcro.X, vRF.CenterY - ctx.Hcro.Y);
            var vHcroLF = Point2D.of(vLF.CenterX - ctx.Hcro.X, vLF.CenterY - ctx.Hcro.Y);

            double dR = Math.Abs(
                CalibrationMath.Distance(Point2D.Zero, ctx.HcroRF) -
                CalibrationMath.Distance(Point2D.Zero, vHcroRF));
            double dL = Math.Abs(
                CalibrationMath.Distance(Point2D.Zero, ctx.HcroLF) -
                CalibrationMath.Distance(Point2D.Zero, vHcroLF));

            if (dR > 2.0 || dL > 2.0)
                _logger.Warning($"HcRO 회전 중심 검증 실패! R={dR:F3}µm  L={dL:F3}µm");
            else
                _logger.Information("HcRO 회전 중심 검증 통과");
        }

        // ═══════════════════════════════════════════════════
        //  Verify: Shift 후 위치 검증
        //   신규 비전 → Clone → 로컬 처리 → ctx 오염 없음
        // ═══════════════════════════════════════════════════

        private async Task VerifyAfterShift(
            AlignContext ctx, double totalShiftX, double totalShiftY, CancellationToken ct)
        {
            var rawRA = await BtmDieVisionRightAlign(ct);
            var rawLA = await BtmDieVisionLeftAlign(ct);

            if (!ctx.HasHcRO) return;

            var vRA = rawRA.Clone();
            var vLA = rawLA.Clone();
            vRA.StageX -= ctx.Hc2Offset.X; vRA.StageY -= ctx.Hc2Offset.Y;
            vLA.StageX -= ctx.Hc1Offset.X; vLA.StageY -= ctx.Hc1Offset.Y;

            vRA = ApplyRotationToCopy(vRA, ctx.Hc2Rad);
            vLA = ApplyRotationToCopy(vLA, ctx.Hc1Rad);

            var vHcroRA = Point2D.of(vRA.CenterX - ctx.Hcro.X, vRA.CenterY - ctx.Hcro.Y);
            var vHcroLA = Point2D.of(vLA.CenterX - ctx.Hcro.X, vLA.CenterY - ctx.Hcro.Y);

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

        // ═══════════════════════════════════════════════════
        //  핵심 유틸: Non-destructive 회전 보정
        //   ★ 기존 Rotate4Points 를 완전 대체
        //   ★ 원본 Clone → DxCamToMark/DyCamToMark 회전 → 새 인스턴스 반환
        //   ★ 원본 객체는 절대 수정하지 않음
        // ═══════════════════════════════════════════════════

        private static VisionMarkResult ApplyRotationToCopy(VisionMarkResult m, double rad)
        {
            var copy = m.Clone();
            var p = CalibrationMath.ApplyRotation(
                Point2D.of(m.DxCamToMark, m.DyCamToMark), rad);
            copy.DxCamToMark = p.X;
            copy.DyCamToMark = p.Y;
            return copy;
        }

        // ★ 기존 Rotate4Points 삭제됨 — in-place 수정으로 누적 오차 원인이었음
        // private static void Rotate4Points(...) { ... }

        // ═══════════════════════════════════════════════════
        //  기타 유틸
        // ═══════════════════════════════════════════════════

        private double ParseDouble(string s)
        {
            s = s.Replace('\u2212', '-')   // minus sign
                 .Replace('\u2013', '-')   // en-dash
                 .Replace('\u00A0', ' ')   // non-breaking space
                 .Trim();
            return double.Parse(s, CultureInfo.InvariantCulture);
        }

        private static double ParseRecipe(RecipeService svc, string paramName)
        {
            var p = svc.FindByParam(paramName);
            return p != null ? double.Parse(p.Value) : 0.0;
        }
    }
}