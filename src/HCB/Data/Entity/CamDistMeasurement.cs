using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCB.Data.Entity
{
    /// <summary>
    /// 카메라 거리(Hc2Offset) 측정 원본. HC1/HC2 각 카메라로 동일 마크를 센터링한 시점의
    /// 모션 스테이지 위치(StageX/Y)와 최종 비전 잔차(DxCam/DyCam), 그리고 결과 거리(Hc2Offset).
    /// PK = FK = BondingRecordId (공유키 1:1). Manual 트레이싱에서만 측정된다.
    /// </summary>
    [Table("CamDistMeasurement")]
    public class CamDistMeasurement
    {
        [Key]
        public int BondingRecordId { get; set; }

        // ── HC1 (모션 스테이지 + 비전 잔차 + 비전 절대 중심) ──
        public double Hc1_StageX { get; set; }
        public double Hc1_StageY { get; set; }
        public double Hc1_DxCam { get; set; }
        public double Hc1_DyCam { get; set; }
        public double Hc1_CenterX { get; set; }
        public double Hc1_CenterY { get; set; }

        // ── HC2 (모션 스테이지 + 비전 잔차 + 비전 절대 중심) ──
        public double Hc2_StageX { get; set; }
        public double Hc2_StageY { get; set; }
        public double Hc2_DxCam { get; set; }
        public double Hc2_DyCam { get; set; }
        public double Hc2_CenterX { get; set; }
        public double Hc2_CenterY { get; set; }

        // ── 결과: 카메라 거리 ──
        public double Hc2Offset_X { get; set; }
        public double Hc2Offset_Y { get; set; }

        public BondingRecord? BondingRecord { get; set; }
    }
}
