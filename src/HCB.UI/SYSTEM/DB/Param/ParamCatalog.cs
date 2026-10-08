using HCB.Data.Entity.Type;
using System;
using System.Collections.Generic;
using System.Linq;
using ValueType = HCB.Data.Entity.Type.ValueType;

namespace HCB.UI
{
    /// <summary>필수 여부. 레시피는 종류(DIE/WAFER)별로 다르게 지정한다. EC 파라미터는 Both/None만 사용.</summary>
    [Flags]
    public enum Req
    {
        None = 0,
        Die = 1,
        Wafer = 2,
        Both = Die | Wafer,
    }

    /// <summary>
    /// 파라미터 이름 → 구역(카테고리), 설명, 필수 여부.
    /// 시퀀스가 이름 문자열로 조회하는 파라미터 목록이며 docs/recipe-ecparam-parameters.md 와 동일하게 유지한다.
    /// <para>Required: 어떤 레시피 종류에서 필수인지. 레시피 생성 시 해당 종류의 필수 항목이 자동 추가되고,
    /// 빠진 필수 항목은 화면에 경고로 표시된다.</para>
    /// <para>Default: 자동 추가할 때 넣는 값.
    /// null이면 빈 값으로 추가되어 사용자가 직접 입력해야 한다(두께·위치 등 0이 위험한 물리값).
    /// 보정 오프셋처럼 0이 "보정 없음"인 항목만 "0"을 지정한다.</para>
    /// </summary>
    public sealed record ParamDef(string Name, string Category, string Description, Req Required = Req.None,
                                  string? Default = null, ValueType Type = ValueType.Double, UnitType Unit = UnitType.mm)
    {
        /// <summary>component가 null이면(EC 등 종류 구분 없음) 어느 종류든 필수면 true</summary>
        public bool IsRequiredFor(ComponentType? component) => component switch
        {
            null => Required != Req.None,
            ComponentType.DIE => Required.HasFlag(Req.Die),
            ComponentType.WAFER => Required.HasFlag(Req.Wafer),
            _ => false,
        };
    }

    public sealed class ParamCatalog
    {
        public const string EtcCategory = "기타";

        private readonly Dictionary<string, (ParamDef Def, int Index)> _map;

        /// <summary>카테고리 표시 순서</summary>
        public IReadOnlyList<string> Categories { get; }
        public IReadOnlyList<ParamDef> Defs { get; }

        private ParamCatalog(IReadOnlyList<ParamDef> defs)
        {
            Defs = defs;
            _map = defs.Select((d, i) => (d, i)).ToDictionary(x => x.d.Name, x => (x.d, x.i));
            Categories = defs.Select(d => d.Category).Distinct().Append(EtcCategory).ToList();
        }

        public ParamDef? Find(string? name)
            => name != null && _map.TryGetValue(name, out var v) ? v.Def : null;

        public int IndexOf(string? name)
            => name != null && _map.TryGetValue(name, out var v) ? v.Index : int.MaxValue;

        public int CategoryOrder(string category)
        {
            int idx = Categories.ToList().IndexOf(category);
            return idx < 0 ? int.MaxValue : idx;
        }

        /// <summary>해당 종류의 필수 항목 (카탈로그 순서)</summary>
        public IEnumerable<ParamDef> RequiredFor(ComponentType? component)
            => Defs.Where(d => d.IsRequiredFor(component));

        // ── Recipe ─────────────────────────────────────────────
        //  DIE  : StepSeq(TopHighAlign → BtmHighAlign → 좌표 통합 → BondingCorr) 흐름에서 조회하는 값
        //  WAFER: WaferSeq(웨이퍼 맵 + ResultMeasurement/SimpleMeasurement) 흐름에서 조회하는 값
        public static ParamCatalog Recipe { get; } = new ParamCatalog(new[]
        {
            new ParamDef("TOP_DIE_THICKNESS", "다이 / 웨이퍼", "Top Die 두께. 본딩·픽업 H_Z 계산", Req.Both),
            new ParamDef("BTM_DIE_THICKNESS", "다이 / 웨이퍼", "Btm Die 두께. 본딩 H_Z 계산", Req.Both),
            new ParamDef("TOP_DIE_SIZE",      "다이 / 웨이퍼", "Top Die 크기 (비전 촬상 위치 예측)", Req.Die),
            new ParamDef("WAFER_SIZE",        "다이 / 웨이퍼", "웨이퍼 크기 (웨이퍼 맵)", Req.Wafer, Type: ValueType.Integer, Unit: UnitType.None),
            new ParamDef("DIE_SIZE_X",        "다이 / 웨이퍼", "다이 크기 X (웨이퍼 맵)", Req.Wafer),
            new ParamDef("DIE_SIZE_Y",        "다이 / 웨이퍼", "다이 크기 Y (웨이퍼 맵)", Req.Wafer),
            new ParamDef("GAP_X",             "다이 / 웨이퍼", "다이 간격 X (웨이퍼 맵)", Req.Wafer),
            new ParamDef("GAP_Y",             "다이 / 웨이퍼", "다이 간격 Y (웨이퍼 맵)", Req.Wafer),
            new ParamDef("SCRIBE_SHIFT_X",    "다이 / 웨이퍼", "스크라이브 라인 시프트 X", Req.Wafer, Default: "0"),
            new ParamDef("SCRIBE_SHIFT_Y",    "다이 / 웨이퍼", "스크라이브 라인 시프트 Y", Req.Wafer, Default: "0"),
            new ParamDef("ALIGN_TOP_SPACING_X", "다이 / 웨이퍼", "Top 얼라인 마크 간격 X", Req.Wafer),
            new ParamDef("ALIGN_TOP_SPACING_Y", "다이 / 웨이퍼", "Top 얼라인 마크 간격 Y", Req.Wafer),

            new ParamDef("SPEC_X",     "판정 스펙", "본딩 결과 판정 스펙 X", Req.Wafer),
            new ParamDef("SPEC_Y",     "판정 스펙", "본딩 결과 판정 스펙 Y", Req.Wafer),
            new ParamDef("SPEC_THETA", "판정 스펙", "θ 목표값 (thetaF = SPEC_THETA − 측정 각도차)", Req.Both, Default: "0", Unit: UnitType.None),

            new ParamDef("FID_ALIGN_GAP",      "정렬 보정", "Fid↔Align 마크 촬상 높이 차 (H_Z 상대 이동량)", Req.Both),
            new ParamDef("X_ALIGN_OFFSET",     "정렬 보정", "최종 보정 X 오프셋 (ResultX에 더함)", Req.Die, Default: "0"),
            new ParamDef("Y_ALIGN_OFFSET",     "정렬 보정", "최종 보정 Y 오프셋 (ResultY에 더함)", Req.Die, Default: "0"),
            new ParamDef("T_ALIGN_OFFSET",     "정렬 보정", "최종 보정 θ 오프셋 (ResultT에 더함)", Req.Die, Default: "0", Unit: UnitType.None),
            new ParamDef("HC_CENTER_ERROR_X",  "정렬 보정", "HC 카메라 센터링 오차 보정 X", Req.Both, Default: "0"),
            new ParamDef("HC_CENTER_ERROR_Y",  "정렬 보정", "HC 카메라 센터링 오차 보정 Y", Req.Both, Default: "0"),
            new ParamDef("LOW_ERROR_OFFSET_X", "정렬 보정", "저배율 비전 픽업 위치 오차 보정 X", Req.Die, Default: "0"),
            new ParamDef("LOW_ERROR_OFFSET_Y", "정렬 보정", "저배율 비전 픽업 위치 오차 보정 Y", Req.Die, Default: "0"),

            new ParamDef("READY_POSITION", "모션 / 본딩", "H_Z 본딩 대기 간격 (Z 하강 식에서 빼는 값)", Req.Die),
            new ParamDef("TOP_BTM_GAP",    "모션 / 본딩", "Top/Btm Die 사이 간격", Req.Both),
            new ParamDef("BONDING_FORCE",  "모션 / 본딩", "본딩 하중"),
            new ParamDef("BONDING_TIME",   "모션 / 본딩", "본딩 시간"),

            new ParamDef("VERNIER_OFFSET_X", "버니어", "버니어 측정 위치 오프셋 X", Req.Die, Default: "0"),
            new ParamDef("VERNIER_OFFSET_Y", "버니어", "버니어 측정 위치 오프셋 Y", Req.Die, Default: "0"),
            new ParamDef("VERNIER_DIST_X",   "버니어", "버니어 패턴 간 거리 X", Req.Die),
            new ParamDef("VERNIER_DIST_Y",   "버니어", "버니어 패턴 간 거리 Y", Req.Die),

            new ParamDef(RecipeService.VisionRecipeParamName, "비전 연동", "비전 PC 레시피 이름 (사용 레시피 변경 시 전송)", Type: ValueType.String, Unit: UnitType.None),
        });

        // ── EC Parameter (레시피 종류와 무관) ───────────────────
        public static ParamCatalog EC { get; } = new ParamCatalog(new[]
        {
            new ParamDef("SHANK_TO_WAFER_OFFSET", "장비 오프셋", "Shank ↔ Wafer 표면 거리 (본딩 H_Z 기준)", Req.Both),
            new ParamDef("SHANK_TO_DIE_OFFSET",   "장비 오프셋", "Shank ↔ Die Carrier 거리 (픽업 H_Z)", Req.Both),
            new ParamDef("SHANK_LOW_OFFSET_X",    "장비 오프셋", "저배율 카메라 ↔ Shank 중심 오프셋 X", Req.Both),
            new ParamDef("SHANK_LOW_OFFSET_Y",    "장비 오프셋", "저배율 카메라 ↔ Shank 중심 오프셋 Y", Req.Both),
            new ParamDef("ALIGN_DIST_TOLERANCE",  "장비 오프셋", "Top/Btm Align 거리 차 허용치 (0 이하: 검사 안 함)"),

            new ParamDef("HC1_X",  "카메라 캘리브레이션", "HC1 카메라 위치 X", Req.Both),
            new ParamDef("HC1_Y",  "카메라 캘리브레이션", "HC1 카메라 위치 Y", Req.Both),
            new ParamDef("HC2_X",  "카메라 캘리브레이션", "HC2 카메라 위치 X (Hc2Offset)", Req.Both),
            new ParamDef("HC2_Y",  "카메라 캘리브레이션", "HC2 카메라 위치 Y (Hc2Offset)", Req.Both),
            new ParamDef("HC1_T",  "카메라 캘리브레이션", "HC1 카메라 회전 각도", Req.Both),
            new ParamDef("HC2_T",  "카메라 캘리브레이션", "HC2 카메라 회전 각도", Req.Both),
            new ParamDef("PC_T",   "카메라 캘리브레이션", "PC 카메라 회전 각도", Req.Both),
            new ParamDef("PC_W_T", "카메라 캘리브레이션", "PC 카메라 ↔ Wafer 각도 (HC1/HC2_T에 더함)", Req.Both),

            new ParamDef("HCRO_X",        "회전중심 (HCRO)", "Head 회전중심 X", Req.Both),
            new ParamDef("HCRO_Y",        "회전중심 (HCRO)", "Head 회전중심 Y", Req.Both),
            new ParamDef("HCRO_FIT_MODE", "회전중심 (HCRO)", "피팅 방식: rigid | circle (기본 rigid)"),
            new ParamDef("HCRO_REPEAT_N", "회전중심 (HCRO)", "HCRO 측정 반복 횟수 (기본 1)"),

            new ParamDef("LOW_VISION_RETRY_MAX", "비전 옵션", "저배율 비전 재시도 횟수 (기본 3)"),
            new ParamDef("VISION_RETRY_MAX",     "비전 옵션", "고배율 비전 재시도 횟수 (기본 3)"),
            new ParamDef("VISION_RETRY_STEP_MM", "비전 옵션", "재시도 이동 간격 mm (기본 0.005)"),
            new ParamDef("BTM_THETA_SIGN",       "비전 옵션", "Btm θ 보정 부호 (기본 −1)"),
            new ParamDef("BTM_THETA_MIN_DEG",    "비전 옵션", "Btm θ 보정 데드밴드 ° (기본 0)"),
        });
    }
}
