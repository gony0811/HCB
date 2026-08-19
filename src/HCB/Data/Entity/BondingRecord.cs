using HCB.Data.Entity.Type;
using HCB.Data.Interface;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCB.Data.Entity
{
    /// <summary>
    /// 본딩 정보(마스터). CSV 1행 = 본딩 1회에 대응한다.
    /// 측정/설비/설정/분석/통합좌표/결과 6개 자식 테이블을 1:1로 소유하며,
    /// 재측정(ReMeasure) 레코드는 ParentRecordId로 원본 Bonding 레코드에 연결된다.
    /// </summary>
    [Table("BondingRecord")]
    public class BondingRecord : IEntity   // Id (PK) 자동
    {
        public DateTime Time { get; set; } = DateTime.Now;

        /// <summary>평균 이동 모드 (AlignData.AvgMove).</summary>
        public bool AvgMode { get; set; }

        public BondingKind Kind { get; set; } = BondingKind.Bonding;

        // ── 측정/보정 모드 플래그 (AlignData) ─────────────────
        public bool Use2DMapping { get; set; }

        /// <summary>TracingMode(Auto/Manual/None). UI enum → 문자열 저장.</summary>
        public string TracingMode { get; set; } = "";

        public bool UseBtmIndividualMeasure { get; set; }
        public bool UseFiducialTracking { get; set; }
        public bool UseRightFidSimilarity { get; set; }

        // ── 재측정 링크 (self-reference) ──────────────────────
        /// <summary>재측정 레코드가 참조하는 원본 Bonding 레코드 Id. 원본이면 null.</summary>
        public int? ParentRecordId { get; set; }
        public BondingRecord? Parent { get; set; }
        public ICollection<BondingRecord> ReMeasures { get; set; } = new List<BondingRecord>();

        // ── 1:1 자식 (검색 시 Include 대상) ───────────────────
        public BondingMeasurement? Measurement { get; set; }
        public BondingEquipment? Equipment { get; set; }
        public BondingSetting? Setting { get; set; }
        public BondingAnalysis? Analysis { get; set; }
        public BondingCoordinate? Coordinate { get; set; }
        public BondingResult? Result { get; set; }
    }
}
