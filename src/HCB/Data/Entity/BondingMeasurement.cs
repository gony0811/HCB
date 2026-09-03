using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCB.Data.Entity
{
    /// <summary>
    /// 측정 정보(비전 원본). Top 4마크는 Stage/DxCam/Center 6항목, Btm 4마크는 DxCam/DyCam만.
    /// PK = FK = BondingRecordId (공유키 1:1). 미측정 마크는 NULL.
    /// </summary>
    [Table("BondingMeasurement")]
    public class BondingMeasurement
    {
        [Key]
        public int BondingRecordId { get; set; }

        // ── Top Right Fiducial ──
        public double? TopRF_StageX { get; set; }
        public double? TopRF_StageY { get; set; }
        public double? TopRF_DxCam { get; set; }
        public double? TopRF_DyCam { get; set; }
        public double? TopRF_CenterX { get; set; }
        public double? TopRF_CenterY { get; set; }

        // ── Top Right Align ──
        public double? TopRA_StageX { get; set; }
        public double? TopRA_StageY { get; set; }
        public double? TopRA_DxCam { get; set; }
        public double? TopRA_DyCam { get; set; }
        public double? TopRA_CenterX { get; set; }
        public double? TopRA_CenterY { get; set; }

        // ── Top Left Fiducial ──
        public double? TopLF_StageX { get; set; }
        public double? TopLF_StageY { get; set; }
        public double? TopLF_DxCam { get; set; }
        public double? TopLF_DyCam { get; set; }
        public double? TopLF_CenterX { get; set; }
        public double? TopLF_CenterY { get; set; }

        // ── Top Left Align ──
        public double? TopLA_StageX { get; set; }
        public double? TopLA_StageY { get; set; }
        public double? TopLA_DxCam { get; set; }
        public double? TopLA_DyCam { get; set; }
        public double? TopLA_CenterX { get; set; }
        public double? TopLA_CenterY { get; set; }

        // ── Btm (HC 카메라 상대거리 DxCam/DyCam) ──
        public double? BtmRF_DxCam { get; set; }
        public double? BtmRF_DyCam { get; set; }
        public double? BtmRA_DxCam { get; set; }
        public double? BtmRA_DyCam { get; set; }
        public double? BtmLF_DxCam { get; set; }
        public double? BtmLF_DyCam { get; set; }
        public double? BtmLA_DxCam { get; set; }
        public double? BtmLA_DyCam { get; set; }

        public BondingRecord? BondingRecord { get; set; }
    }
}
