using System;
using System.Collections.Generic;
using System.Drawing.Text;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Markup;
using System.Xml.Linq;

namespace HCB.UI
{
    public class MessageHeader
    {
        public string? MessageName { get; set; }
        public string? UnitName { get; set; }
        public DateTime? Time { get; set; }

        public XElement ToXml()
        {
            return new XElement("HEADER",
                new XElement("MESSAGENAME", MessageName ?? string.Empty),
                new XElement("UNITNAME", UnitName ?? string.Empty),
                new XElement("TIME", Time?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty)
            );
        }

        public static MessageHeader FromXml(XElement element)
        {
            return new MessageHeader
            {
                MessageName = element.Element("MESSAGENAME")?.Value,
                UnitName = element.Element("UNITNAME")?.Value,
                Time = DateTime.TryParse(element.Element("TIME")?.Value ?? string.Empty, out var dateTime)
                    ? dateTime
                    : null
            };
        }
    }

    public class MessageData
    {
        public string? Content { get; set; }

        public XElement ToXml()
        {
            return new XElement("DATA", Content ?? string.Empty);
        }

        public static MessageData FromXml(XElement element)
        {
            return new MessageData
            {
                Content = element.Value
            };
        }
    }

    public class Message
    {
        public MessageHeader? Header { get; set; }
        public MessageData? Data { get; set; }
        public string? Tail { get; set; }

        public Message()
        {
            Header = new MessageHeader();
            Data = new MessageData();
            Tail = "</MESSAGE>";
        }

        /// <summary>
        /// Converts the message to XML format
        /// </summary>
        public string ToXml()
        {
            var root = new XElement("MESSAGE",
                Header?.ToXml(),
                Data?.ToXml(),
                new XElement("TAIL", Tail ?? string.Empty)
            );
            return "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" + root.ToString();
        }

        /// <summary>
        /// Parses an XML string and creates a Message object
        /// </summary>
        public static Message FromXml(string xmlString)
        {
            try
            {
                var doc = XDocument.Parse(xmlString);
                var root = doc.Root;
                if (root?.Name != "MESSAGE")
                    throw new InvalidOperationException("Root element must be MESSAGE");

                var message = new Message
                {
                    Header = root.Element("HEADER") != null
                        ? MessageHeader.FromXml(root.Element("HEADER")!)
                        : new MessageHeader(),
                    Data = root.Element("DATA") != null
                        ? MessageData.FromXml(root.Element("DATA")!)
                        : new MessageData(),
                    Tail = root.Element("TAIL")?.Value
                };

                return message;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing XML: {ex.Message}");
                return new Message();
            }
        }

        public override string ToString()
        {
            return $"Message: {Header?.MessageName}, Unit: {Header?.UnitName}, Time: {Header?.Time}, Data: {Data?.Content}";
        }
    }

    public class VisionMarkPositionResponse
    {
        //public MarkType MarkType { get; set; }
        //public CameraType CameraType { get; set; }
        public Result Result { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Theta { get; set; }
        //public double Score { get; set; }

        public static VisionMarkPositionResponse Parse(string? content)
        {
            var response = new VisionMarkPositionResponse();
            if (string.IsNullOrEmpty(content)) return response;

            try
            {
                var xml = XElement.Parse($"<DATA>{content}</DATA>");

                //if (Enum.TryParse(xml.Element("MARKTYPE")?.Value, out MarkType mt)) response.MarkType = mt;
                //if (Enum.TryParse(xml.Element("CAMERATYPE")?.Value, out CameraType ct)) response.CameraType = ct;
                if (Enum.TryParse(xml.Element("RESULT")?.Value, out Result r)) response.Result = r;

                if (double.TryParse(xml.Element("X")?.Value, out double x)) response.X = x;
                if (double.TryParse(xml.Element("Y")?.Value, out double y)) response.Y = y;
                if (double.TryParse(xml.Element("THETA")?.Value, out double theta)) response.Theta = theta;
                //if (double.TryParse(xml.Element("SCORE")?.Value, out double score)) response.Score = score;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VisionMarkPositionResponse] 파싱 오류: {ex.Message}");
            }

            return response;
        }
    }


    public class BtmMarkResponse
    {
        public Result Result { get; set; } = Result.NG;

        // ── LEFT ──
        public Result LeftFidAf { get; set; } = Result.NG;
        public Result LeftFidResult { get; set; } = Result.NG;
        public Point2D LeftFid { get; set; } = new Point2D(0, 0);
        public Result LeftAlignMove { get; set; } = Result.NG;
        public Result LeftAlignAf { get; set; } = Result.NG;
        public Result LeftAlignResult { get; set; } = Result.NG;
        public Point2D LeftAlign { get; set; } = new Point2D(0, 0);

        // ── RIGHT ──
        public Result RightFidAf { get; set; } = Result.NG;
        public Result RightFidResult { get; set; } = Result.NG;
        public Point2D RightFid { get; set; } = new Point2D(0, 0);
        public Result RightAlignMove { get; set; } = Result.NG;
        public Result RightAlignAf { get; set; } = Result.NG;
        public Result RightAlignResult { get; set; } = Result.NG;
        public Point2D RightAlign { get; set; } = new Point2D(0, 0);

        public static BtmMarkResponse Parse(string? content)
        {
            var response = new BtmMarkResponse();
            if (string.IsNullOrWhiteSpace(content))
                return response; // 빈 응답 → Result.NG, 좌표 (0,0)

            var root = XElement.Parse($"<DATA>{content}</DATA>");

            response.Result = ParseResult(root.Element("RESULT")?.Value);

            // SIDES=LEFT/RIGHT 요청 시 미요청 측 블록은 생략됨 → null이면 기본값 유지
            var left = root.Element("LEFT");
            if (left != null)
            {
                response.LeftFidAf = ParseResult(left.Element("FID_AF")?.Value);
                response.LeftFidResult = ParseResult(left.Element("FID_RESULT")?.Value);
                response.LeftFid = ParsePoint(left, "FID_X", "FID_Y");
                response.LeftAlignMove = ParseResult(left.Element("ALIGN_MOVE")?.Value);
                response.LeftAlignAf = ParseResult(left.Element("ALIGN_AF")?.Value);
                response.LeftAlignResult = ParseResult(left.Element("ALIGN_RESULT")?.Value);
                response.LeftAlign = ParsePoint(left, "ALIGN_X", "ALIGN_Y");
            }

            var right = root.Element("RIGHT");
            if (right != null)
            {
                response.RightFidAf = ParseResult(right.Element("FID_AF")?.Value);
                response.RightFidResult = ParseResult(right.Element("FID_RESULT")?.Value);
                response.RightFid = ParsePoint(right, "FID_X", "FID_Y");
                response.RightAlignMove = ParseResult(right.Element("ALIGN_MOVE")?.Value);
                response.RightAlignAf = ParseResult(right.Element("ALIGN_AF")?.Value);
                response.RightAlignResult = ParseResult(right.Element("ALIGN_RESULT")?.Value);
                response.RightAlign = ParsePoint(right, "ALIGN_X", "ALIGN_Y");
            }

            return response;
        }

        private static Result ParseResult(string? value)
            => string.Equals(value?.Trim(), "OK", StringComparison.OrdinalIgnoreCase)
                ? Result.OK
                : Result.NG;

        private static Point2D ParsePoint(XElement side, string xTag, string yTag)
        {
            double x = double.TryParse(side.Element(xTag)?.Value, NumberStyles.Any,
                CultureInfo.InvariantCulture, out var xv) ? xv : 0;
            double y = double.TryParse(side.Element(yTag)?.Value, NumberStyles.Any,
                CultureInfo.InvariantCulture, out var yv) ? yv : 0;
            return new Point2D(x, y);
        }
    }
    public class VernierResponse
    {
        //public MarkType MarkType { get; set; }
        //public CameraType CameraType { get; set; }
        public Result Result { get; set; }
        public double Value_1 { get; set; }
        public double Value_3 { get; set; }

        public static VernierResponse Parse(string? content)
        {
            var response = new VernierResponse();
            if (string.IsNullOrEmpty(content)) return response;

            try
            {
                var xml = XElement.Parse($"<DATA>{content}</DATA>");

                if (Enum.TryParse(xml.Element("RESULT")?.Value, out Result r)) response.Result = r;

                if (double.TryParse(xml.Element("VALUE1")?.Value, out double value1)) response.Value_1 = value1;
                if (double.TryParse(xml.Element("VALUE3")?.Value, out double value3)) response.Value_3 = value3;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[VernierResponse] 파싱 오류: {ex.Message}");
            }

            return response;
        }
    }
    // 스크라이브 교차점(십자) 검출 응답. Vision 회신 v1.0 확정 규약.
    // X/Y = 교차점의 카메라 중심 대비 오프셋(mm). THETA는 규약에서 제외(각도는 EQP가 atan2로 산출).
    public class ScribeLineResponse
    {
        public Result Result { get; set; } = Result.NG;
        public double X { get; set; }
        public double Y { get; set; }

        public static ScribeLineResponse Parse(string? content)
        {
            var response = new ScribeLineResponse();
            if (string.IsNullOrEmpty(content)) return response;

            try
            {
                var xml = XElement.Parse($"<DATA>{content}</DATA>");

                if (Enum.TryParse(xml.Element("RESULT")?.Value, out Result r)) response.Result = r;
                if (double.TryParse(xml.Element("X")?.Value, out double x)) response.X = x;
                if (double.TryParse(xml.Element("Y")?.Value, out double y)) response.Y = y;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ScribeLineResponse] 파싱 오류: {ex.Message}");
            }

            return response;
        }
    }
    public enum DieType
    {
        TOP,
        BOTTOM
    }
    public enum DirectType
    {
        LEFT,
        RIGHT,
        BOTH,
        Vertical,
        Horizontal
    }
    public enum MarkType
    {
        DIE_CENTER_TOP,
        DIE_CENTER_BOTTOM,
        DIE_CENTER,
        FIDUCIAL,
        CORNER,
        ALIGN_MARK,
        ALIGN_MARK_TOP,
        VERNIER,
        WAFER_EDGE,   // 저배율(HC_LOW) 웨이퍼 엣지 검출용. 기존 값 보존 위해 끝에 추가.
        SCRIBE_LINE   // 고배율(HC1/HC2) 스크라이브 교차점 AF/검출용. REQUEST_AF_START MARKTYPE 신규값.
    }

    public enum CameraType
    {
        HC_LOW,
        HC1_HIGH,
        HC2_HIGH,
        PC_LOW,
        PC_HIGH
    }

    // 웨이퍼 엣지 검출 시계 위치. 물리 위치는 11/4/7시이며, 비전에는 프로토콜 코드로 직렬화한다.
    // (11시 위치의 비전 통신 코드는 12) XML에는 정수로 직렬화.
    public enum WaferClock
    {
        H11 = 12,   // 물리 11시 위치 — 비전 통신 코드는 12
        H04 = 4,
        H07 = 7
    }

    public enum TracingMode
    {
        Auto,
        Manual,
        None
    }

    public enum Result
    {
        OK,
        NG
    }
    public class EnumValues : MarkupExtension
    {
        private readonly Type _enumType;
        public EnumValues(Type enumType) => _enumType = enumType;
        public override object ProvideValue(IServiceProvider serviceProvider)
            => Enum.GetValues(_enumType);
    }

    public class MotionMoveCommand
    {
        public string Axis { get; init; }
        public string Direction { get; init; } 
        public double Distance { get; init; }
    }
    public class MotionMoveResult
    {
        //public string Axis { get; set; }
        //public string Direction { get; set; }
        //public double Distance { get; set; }
        public bool Result { get; set; }

        public string ToXml() =>
            //$"<AXIS>{Axis}</AXIS>" +
            //$"<DIRECTION>{Direction}</DIRECTION>" +
            //$"<DISTANCE>{Distance}</DISTANCE>" +
            $"<RESULT>{(Result ? "OK" : "NG")}</RESULT>";
    }
}
