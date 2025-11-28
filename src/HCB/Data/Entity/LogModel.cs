using HCB.Data.Interface;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HCB.Data.Entity
{
    public class LogModel : IEntity
    {
        public DateTimeOffset Timestamp { get; set; }

        // ★ 추가: 트레이스 ID (정수)
        public int ThreadId { get; set; }

        // ★ 추가: 로깅 출처 (클래스/네임스페이스)
        [MaxLength(256)]
        public string? SourceContext { get; set; }

        [MaxLength(16)]
        public string? Level { get; set; }

        [Required]
        public string? Message { get; set; }

        // Exception 정보 (기존)
        public string? Exception { get; set; }
    }
}
