using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Telerik.Windows.Controls;
using static HCB.UI.SERVICE.CalibrationService;

namespace HCB.UI
{
    public partial class TopHighAlignInfoWindow : RadWindow
    {
        private readonly VisionMarkResult _rightFid;
        private readonly VisionMarkResult _rightAlign;
        private readonly VisionMarkResult _leftFid;
        private readonly VisionMarkResult _leftAlign;
        private readonly bool _useWaferY;

        public TopHighAlignInfoWindow(
            VisionMarkResult rightFid,
            VisionMarkResult rightAlign,
            VisionMarkResult leftFid,
            VisionMarkResult leftAlign,
            bool useWaferY = false)
        {
            InitializeComponent();
            _rightFid   = rightFid;
            _rightAlign = rightAlign;
            _leftFid    = leftFid;
            _leftAlign  = leftAlign;
            _useWaferY  = useWaferY;

            Loaded += (s, e) => UpdateDisplay();
        }

        // CenterY / CenterWaferY 분기 헬퍼
        private double GetY(VisionMarkResult m) =>
            _useWaferY ? m.CenterWaferY : m.CenterY;

        // ── 좌표 텍스트 업데이트 ──────────────────────────────────
        private void UpdateDisplay()
        {
            // Center
            RightFidX.Text   = _rightFid?.CenterX.ToString("F4")          ?? "N/A";
            RightFidY.Text   = _rightFid   != null ? GetY(_rightFid).ToString("F4")   : "N/A";
            RightAlignX.Text = _rightAlign?.CenterX.ToString("F4")        ?? "N/A";
            RightAlignY.Text = _rightAlign != null ? GetY(_rightAlign).ToString("F4") : "N/A";
            LeftFidX.Text    = _leftFid?.CenterX.ToString("F4")           ?? "N/A";
            LeftFidY.Text    = _leftFid    != null ? GetY(_leftFid).ToString("F4")    : "N/A";
            LeftAlignX.Text  = _leftAlign?.CenterX.ToString("F4")         ?? "N/A";
            LeftAlignY.Text  = _leftAlign  != null ? GetY(_leftAlign).ToString("F4")  : "N/A";

            // Stage
            RightFidStageX.Text   = _rightFid?.StageX.ToString("F4")   ?? "N/A";
            RightFidStageY.Text   = _rightFid?.StageY.ToString("F4")   ?? "N/A";
            RightAlignStageX.Text = _rightAlign?.StageX.ToString("F4") ?? "N/A";
            RightAlignStageY.Text = _rightAlign?.StageY.ToString("F4") ?? "N/A";
            LeftFidStageX.Text    = _leftFid?.StageX.ToString("F4")    ?? "N/A";
            LeftFidStageY.Text    = _leftFid?.StageY.ToString("F4")    ?? "N/A";
            LeftAlignStageX.Text  = _leftAlign?.StageX.ToString("F4")  ?? "N/A";
            LeftAlignStageY.Text  = _leftAlign?.StageY.ToString("F4")  ?? "N/A";

            // Cam → Mark Offset
            RightFidDx.Text   = _rightFid?.DxCamToMark.ToString("F4")   ?? "N/A";
            RightFidDy.Text   = _rightFid?.DyCamToMark.ToString("F4")   ?? "N/A";
            RightAlignDx.Text = _rightAlign?.DxCamToMark.ToString("F4") ?? "N/A";
            RightAlignDy.Text = _rightAlign?.DyCamToMark.ToString("F4") ?? "N/A";
            LeftFidDx.Text    = _leftFid?.DxCamToMark.ToString("F4")    ?? "N/A";
            LeftFidDy.Text    = _leftFid?.DyCamToMark.ToString("F4")    ?? "N/A";
            LeftAlignDx.Text  = _leftAlign?.DxCamToMark.ToString("F4")  ?? "N/A";
            LeftAlignDy.Text  = _leftAlign?.DyCamToMark.ToString("F4")  ?? "N/A";

            // 중심 좌표 & Offset
            bool hasFidCenter   = _rightFid   != null && _leftFid   != null;
            bool hasAlignCenter = _rightAlign  != null && _leftAlign != null;

            double fidCX = hasFidCenter   ? (_rightFid.CenterX   + _leftFid.CenterX)   / 2.0 : 0;
            double fidCY = hasFidCenter   ? (GetY(_rightFid)     + GetY(_leftFid))     / 2.0 : 0;
            double algCX = hasAlignCenter ? (_rightAlign.CenterX + _leftAlign.CenterX) / 2.0 : 0;
            double algCY = hasAlignCenter ? (GetY(_rightAlign)   + GetY(_leftAlign))   / 2.0 : 0;

            FidCenterX.Text   = hasFidCenter   ? fidCX.ToString("F4") : "N/A";
            FidCenterY.Text   = hasFidCenter   ? fidCY.ToString("F4") : "N/A";
            AlignCenterX.Text = hasAlignCenter ? algCX.ToString("F4") : "N/A";
            AlignCenterY.Text = hasAlignCenter ? algCY.ToString("F4") : "N/A";

            if (hasFidCenter && hasAlignCenter)
            {
                CenterOffsetX.Text = (fidCX - algCX).ToString("+0.0000;-0.0000;0.0000");
                CenterOffsetY.Text = (fidCY - algCY).ToString("+0.0000;-0.0000;0.0000");
            }
            else
            {
                CenterOffsetX.Text = CenterOffsetY.Text = "N/A";
            }

            double theta    = CalculateTheta();
            double thetaFid = CalculateThetaFid();
            ThetaValue.Text    = theta.ToString("F4")    + " °";
            ThetaFidValue.Text = thetaFid.ToString("F4") + " °";

            DrawGraph();
        }

        // ── Theta 계산: L.Align → R.Align 벡터의 수평 기준 각도 ──
        private double CalculateTheta()
        {
            if (_rightAlign == null || _leftAlign == null) return 0.0;
            double dx = _rightAlign.CenterX - _leftAlign.CenterX;
            double dy = GetY(_rightAlign)   - GetY(_leftAlign);
            return Math.Atan2(dy, dx) * 180.0 / Math.PI;
        }

        // ── Fiducial Theta 계산: L.Fid → R.Fid 벡터의 수평 기준 각도 ──
        private double CalculateThetaFid()
        {
            if (_rightFid == null || _leftFid == null) return 0.0;
            double dx = _rightFid.CenterX - _leftFid.CenterX;
            double dy = GetY(_rightFid)   - GetY(_leftFid);
            return Math.Atan2(dy, dx) * 180.0 / Math.PI;
        }

        private void GraphCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (IsLoaded) DrawGraph();
        }

        // ── 그래프 그리기 ──────────────────────────────────────────
        private void DrawGraph()
        {
            GraphCanvas.Children.Clear();

            double w = GraphCanvas.ActualWidth;
            double h = GraphCanvas.ActualHeight;
            if (w <= 0 || h <= 0) return;

            // 유효 마크 수집
            var marks = new List<(string name, double x, double y, Color color)>();
            if (_rightFid   != null) marks.Add(("R.Fid",   _rightFid.CenterX,   GetY(_rightFid),   Colors.Yellow));
            if (_rightAlign != null) marks.Add(("R.Align", _rightAlign.CenterX, GetY(_rightAlign), Colors.Cyan));
            if (_leftFid    != null) marks.Add(("L.Fid",   _leftFid.CenterX,    GetY(_leftFid),    Colors.Orange));
            if (_leftAlign  != null) marks.Add(("L.Align", _leftAlign.CenterX,  GetY(_leftAlign),  Colors.LimeGreen));

            if (marks.Count == 0)
            {
                AddNoDataLabel(w, h);
                return;
            }

            // 좌표 범위 계산 + 여백
            double minX = marks.Min(m => m.x);
            double maxX = marks.Max(m => m.x);
            double minY = marks.Min(m => m.y);
            double maxY = marks.Max(m => m.y);

            double bufX = Math.Max((maxX - minX) * 0.4, 0.5);
            double bufY = Math.Max((maxY - minY) * 0.4, 0.5);
            minX -= bufX; maxX += bufX;
            minY -= bufY; maxY += bufY;

            double rangeX = maxX - minX;
            double rangeY = maxY - minY;

            // 균일 스케일 (종횡비 유지) + 중앙 정렬
            const double pad = 55;
            double drawW = w - 2 * pad;
            double drawH = h - 2 * pad;
            double scale = Math.Min(drawW / rangeX, drawH / rangeY);

            double offsetX = pad + (drawW - rangeX * scale) / 2.0;
            double offsetY = pad + (drawH - rangeY * scale) / 2.0;

            double ToCanvasX(double x) => offsetX + (x - minX) * scale;
            double ToCanvasY(double y) => h - offsetY - (y - minY) * scale; // Y 반전

            // ── 격자선 ──────────────────────────────────────────────
            var gridColor = Color.FromArgb(35, 150, 180, 220);
            const int gridDivisions = 4;
            for (int i = 0; i <= gridDivisions; i++)
            {
                double t = (double)i / gridDivisions;

                double gx = minX + t * rangeX;
                double gcx = ToCanvasX(gx);
                AddLine(gcx, ToCanvasY(minY), gcx, ToCanvasY(maxY), gridColor, 1);
                AddText(gcx - 12, ToCanvasY(minY) + 4, gx.ToString("F2"),
                        Color.FromArgb(120, 140, 170, 200), 9);

                double gy = minY + t * rangeY;
                double gcy = ToCanvasY(gy);
                AddLine(ToCanvasX(minX), gcy, ToCanvasX(maxX), gcy, gridColor, 1);
                AddText(ToCanvasX(minX) - 50, gcy - 7, gy.ToString("F2"),
                        Color.FromArgb(120, 140, 170, 200), 9);
            }

            // 축 레이블
            AddText(w / 2 - 4, ToCanvasY(minY) + 16, "X", Color.FromArgb(160, 160, 190, 220), 10);
            AddText(ToCanvasX(minX) - 48, pad - 14,  "Y", Color.FromArgb(160, 160, 190, 220), 10);

            // ── Fid → Align 연결선 (점선) ───────────────────────────
            if (_rightFid != null && _rightAlign != null)
                AddLine(ToCanvasX(_rightFid.CenterX),   ToCanvasY(_rightFid.CenterY),
                        ToCanvasX(_rightAlign.CenterX), ToCanvasY(_rightAlign.CenterY),
                        Color.FromArgb(160, 180, 180, 80), 1.2, isDashed: true);

            if (_leftFid != null && _leftAlign != null)
                AddLine(ToCanvasX(_leftFid.CenterX),   ToCanvasY(_leftFid.CenterY),
                        ToCanvasX(_leftAlign.CenterX), ToCanvasY(_leftAlign.CenterY),
                        Color.FromArgb(160, 200, 140, 60), 1.2, isDashed: true);

            // ── Fiducial 기준선 L.Fid → R.Fid (주황 실선) ───────────
            if (_leftFid != null && _rightFid != null)
            {
                AddLine(ToCanvasX(_leftFid.CenterX),  ToCanvasY(_leftFid.CenterY),
                        ToCanvasX(_rightFid.CenterX), ToCanvasY(_rightFid.CenterY),
                        Color.FromArgb(200, 255, 170, 40), 1.6);

                double thetaFid = CalculateThetaFid();
                double fidOrigX = ToCanvasX(_leftFid.CenterX);
                double fidOrigY = ToCanvasY(_leftFid.CenterY);

                // 수평 기준선
                AddLine(fidOrigX, fidOrigY, fidOrigX + 70, fidOrigY,
                        Color.FromArgb(130, 150, 150, 150), 1, isDashed: true);

                // Fid Theta 호 (안쪽, 작은 반경)
                DrawThetaArc(fidOrigX, fidOrigY, 28, thetaFid,
                             Color.FromArgb(210, 255, 190, 60));

                double midRadFid = thetaFid / 2.0 * Math.PI / 180.0;
                double lfx = fidOrigX + 42 * Math.Cos(midRadFid);
                double lfy = fidOrigY - 42 * Math.Sin(midRadFid);
                AddText(lfx + 2, lfy - 8, $"θFid={thetaFid:F3}°",
                        Color.FromArgb(255, 255, 200, 80), 10);
            }

            // ── 정렬 기준선 L.Align → R.Align (파란 실선) ────────────
            if (_leftAlign != null && _rightAlign != null)
            {
                AddLine(ToCanvasX(_leftAlign.CenterX),  ToCanvasY(_leftAlign.CenterY),
                        ToCanvasX(_rightAlign.CenterX), ToCanvasY(_rightAlign.CenterY),
                        Colors.DeepSkyBlue, 2.0);

                // ── Align Theta 호(Arc) 및 수평 기준선 ────────────────
                double theta   = CalculateTheta();
                double originX = ToCanvasX(_leftAlign.CenterX);
                double originY = ToCanvasY(_leftAlign.CenterY);

                // 수평 기준선
                AddLine(originX, originY, originX + 70, originY,
                        Color.FromArgb(160, 150, 150, 150), 1, isDashed: true);

                // Align Theta 호 (바깥쪽, 큰 반경)
                DrawThetaArc(originX, originY, 42, theta,
                             Color.FromArgb(210, 255, 220, 60));

                // Theta 레이블 (호 중간 방향)
                double midRad = theta / 2.0 * Math.PI / 180.0;
                double lx = originX + 58 * Math.Cos(midRad);
                double ly = originY - 58 * Math.Sin(midRad);
                AddText(lx + 2, ly - 8, $"θ={theta:F3}°", Colors.Gold, 11);
            }

            // ── 마크 포인트 ──────────────────────────────────────────
            const double r = 7;
            foreach (var (name, x, y, color) in marks)
            {
                double cx = ToCanvasX(x);
                double cy = ToCanvasY(y);
                AddPoint(cx, cy, r, color);
                AddText(cx + r + 3, cy - 10, name,
                        color, 11);
                AddText(cx + r + 3, cy + 3,
                        $"({x:F3}, {y:F3})",
                        Color.FromArgb(180, 160, 200, 230), 9);
            }
        }

        // ── Theta 호 그리기 ──────────────────────────────────────────
        private void DrawThetaArc(double cx, double cy, double radius, double thetaDeg,
                                  Color? strokeColor = null)
        {
            if (Math.Abs(thetaDeg) < 1e-4) return;

            Color color = strokeColor ?? Color.FromArgb(210, 255, 220, 60);

            double startRad = 0;
            double endRad   = thetaDeg * Math.PI / 180.0;

            double x1 = cx + radius * Math.Cos(startRad);
            double y1 = cy - radius * Math.Sin(startRad); // Y 반전
            double x2 = cx + radius * Math.Cos(endRad);
            double y2 = cy - radius * Math.Sin(endRad);

            bool isLargeArc = Math.Abs(thetaDeg) > 180.0;
            var sweep = thetaDeg > 0 ? SweepDirection.Counterclockwise : SweepDirection.Clockwise;

            var arcSeg = new ArcSegment
            {
                Point          = new Point(x2, y2),
                Size           = new Size(radius, radius),
                IsLargeArc     = isLargeArc,
                SweepDirection = sweep,
                RotationAngle  = 0
            };

            var fig = new PathFigure { StartPoint = new Point(x1, y1), IsClosed = false };
            fig.Segments.Add(arcSeg);

            var geo = new PathGeometry();
            geo.Figures.Add(fig);

            GraphCanvas.Children.Add(new System.Windows.Shapes.Path
            {
                Data            = geo,
                Stroke          = new SolidColorBrush(color),
                StrokeThickness = 1.8,
                Fill            = Brushes.Transparent
            });
        }

        // ── 헬퍼 ─────────────────────────────────────────────────────
        private void AddLine(double x1, double y1, double x2, double y2,
                             Color color, double thickness, bool isDashed = false)
        {
            var line = new Line
            {
                X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                Stroke          = new SolidColorBrush(color),
                StrokeThickness = thickness
            };
            if (isDashed) line.StrokeDashArray = new DoubleCollection { 4, 3 };
            GraphCanvas.Children.Add(line);
        }

        private void AddPoint(double cx, double cy, double radius, Color color)
        {
            var e = new Ellipse
            {
                Width           = radius * 2,
                Height          = radius * 2,
                Fill            = new SolidColorBrush(color),
                Stroke          = new SolidColorBrush(Colors.White),
                StrokeThickness = 1
            };
            Canvas.SetLeft(e, cx - radius);
            Canvas.SetTop(e,  cy - radius);
            GraphCanvas.Children.Add(e);
        }

        private void AddText(double x, double y, string text, Color color, double fontSize)
        {
            var tb = new TextBlock
            {
                Text       = text,
                Foreground = new SolidColorBrush(color),
                FontSize   = fontSize
            };
            Canvas.SetLeft(tb, x);
            Canvas.SetTop(tb,  y);
            GraphCanvas.Children.Add(tb);
        }

        private void AddNoDataLabel(double w, double h)
        {
            var tb = new TextBlock
            {
                Text          = "데이터 없음\n고배율 보정을 먼저 수행하세요.",
                Foreground    = new SolidColorBrush(Color.FromArgb(180, 150, 170, 190)),
                FontSize      = 14,
                TextAlignment = TextAlignment.Center
            };
            Canvas.SetLeft(tb, w / 2 - 110);
            Canvas.SetTop(tb,  h / 2 - 20);
            GraphCanvas.Children.Add(tb);
        }
    }
}
