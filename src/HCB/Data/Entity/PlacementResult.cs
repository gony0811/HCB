using HCB.Data.Interface;
using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace HCB.Data.Entity
{
    /// <summary>
    /// 본딩 후 배치 검증 결과(Die 1개 = 1행). Hc1 단일 카메라로 Top/Btm Align 4점을 측정해
    /// 산출한 배치 오차(ErrorX/ErrorY/ErrorTheta)를 Die(Row,Col) 단위로 저장한다.
    /// 같은 Die를 재검증하면 새 행이 추가되며, 최신 행이 현재 결과다.
    /// </summary>
    [Table("PlacementResult")]
    public class PlacementResult : IEntity   // Id (PK) 자동
    {
        public DateTime Time { get; set; } = DateTime.Now;

        // 대상 Die
        public int Row { get; set; }
        public int Col { get; set; }

        // 배치 오차 (ErrorX/ErrorY = µm, ErrorTheta = deg)
        public double ErrorX { get; set; }
        public double ErrorY { get; set; }
        public double ErrorTheta { get; set; }
    }
}
