using System;
using System.Collections.Generic;
using System.Drawing.Text;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
    public enum DieType
    {
        TOP,
        BOTTOM
    }
    public enum DirectType
    {
        LEFT,
        RIGHT
    }
    public enum MarkType
    {
        DIE_CENTER_TOP,
        DIE_CENTER_BOTTOM,
        DIE_CENTER,
        FIDUCIAL,
        CORNER,
        ALIGN_MARK
    }
    
    public enum CameraType
    {
        HC_LOW,
        HC1_HIGH,
        HC2_HIGH,
        PC_LOW,
        PC_HIGH
    }

    public enum Result
    {
        OK, 
        NG
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
