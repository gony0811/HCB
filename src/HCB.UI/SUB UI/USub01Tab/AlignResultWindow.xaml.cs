using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Telerik.Windows.Controls;
using static HCB.UI.SERVICE.CalibrationService;

namespace HCB.UI
{
    public partial class AlignResultWindow : RadWindow
    {
        private readonly Func<AlignData> _dataProvider;
        private readonly double _refAlignDist;
        private readonly double _refFidDist;
        private readonly DispatcherTimer _timer;

        /// <summary>
        /// 실시간 모드: dataProvider 콜백으로 매 갱신마다 최신 AlignData를 가져옴
        /// </summary>
        public AlignResultWindow(Func<AlignData> dataProvider,
                                 double refAlignDist = double.NaN,
                                 double refFidDist = double.NaN)
        {
            InitializeComponent();
            _dataProvider = dataProvider;
            _refAlignDist = refAlignDist;
            _refFidDist = refFidDist;

            Header = "정렬 결과 — 회전 중심 좌표계";

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _timer.Tick += (s, e) => UpdateAll();

            Loaded += (s, e) => { UpdateAll(); _timer.Start(); };
            Closed += (s, e) => _timer.Stop();
        }

        /// <summary>
        /// 스냅샷 모드: 고정된 AlignData 한 장만 표시 (기존 호환)
        /// </summary>
        public AlignResultWindow(AlignData data,
                                 double refAlignDist = double.NaN,
                                 double refFidDist = double.NaN)
            : this(() => data, refAlignDist, refFidDist)
        {
        }

        private void UpdateAll()
        {
            var data = _dataProvider();
            UpdateDistancePanel(data);
            DrawGraph(data);
        }

        // ════════════════════════════════════════════════════
        //  우측 패널: 선분 길이 & 오차
        // ════════════════════════════════════════════════════

        private void UpdateDistancePanel(AlignData data)
        {
            if (data == null)
            {
                TopAlignDistText.Text = BtmAlignDistText.Text = "N/A";
                TopFidDistText.Text = BtmFidDistText.Text = "N/A";
                TopAlignRefText.Text = BtmAlignRefText.Text = "—";
                TopFidRefText.Text = BtmFidRefText.Text = "—";
                TopAlignErrText.Text = BtmAlignErrText.Text = "—";
                TopFidErrText.Text = BtmFidErrText.Text = "—";
                TopDeltaText.Text = BtmDeltaText.Text = AlignDiffText.Text = "N/A";
                ResultXText.Text = ResultYText.Text = ResultTText.Text = "—";
                return;
            }

            // ── 개별 항목 ──
            SetDistRow(TopAlignDistText, TopAlignRefText, TopAlignErrText,
                       data.TopAlignDist, _refAlignDist);
            SetDistRow(BtmAlignDistText, BtmAlignRefText, BtmAlignErrText,
                       data.BtmAlignDist, _refAlignDist);
            SetDistRow(TopFidDistText, TopFidRefText, TopFidErrText,
                       data.TopFidDist, _refFidDist);
            SetDistRow(BtmFidDistText, BtmFidRefText, BtmFidErrText,
                       data.BtmFidDist, _refFidDist);

            // ── 오차 요약 ──
            if (data.TopFidDist > 0 && data.TopAlignDist > 0)
            {
                double d = data.TopFidDist - data.TopAlignDist;
                TopDeltaText.Text = FormatErr(d);
                TopDeltaText.Foreground = ErrBrush(d);
            }
            else TopDeltaText.Text = "N/A";

            if (data.BtmFidDist > 0 && data.BtmAlignDist > 0)
            {
                double d = data.BtmFidDist - data.BtmAlignDist;
                BtmDeltaText.Text = FormatErr(d);
                BtmDeltaText.Foreground = ErrBrush(d);
            }
            else BtmDeltaText.Text = "N/A";

            if (data.TopAlignDist > 0 && data.BtmAlignDist > 0)
            {
                double d = data.TopAlignDist - data.BtmAlignDist;
                AlignDiffText.Text = FormatErr(d);
                AlignDiffText.Foreground = ErrBrush(d);
            }
            else AlignDiffText.Text = "N/A";

            // ── 보정 결과 ──
            ResultXText.Text = data.ResultX.ToString("+0.0000;-0.0000;0.0000") + " mm";
            ResultYText.Text = data.ResultY.ToString("+0.0000;-0.0000;0.0000") + " mm";
            ResultTText.Text = data.ResultT.ToString("+0.0000;-0.0000;0.0000") + " °";
        }

        private void SetDistRow(TextBlock measTb, TextBlock refTb, TextBlock errTb,
                                double measured, double reference)
        {
            if (measured > 0)
            {
                measTb.Text = $"{measured:F4} mm";

                if (!double.IsNaN(reference) && reference > 0)
                {
                    refTb.Text = $"{reference:F4} mm";
                    double err = measured - reference;
                    double errUm = err * 1000.0;
                    errTb.Text = $"{err:+0.0000;-0.0000;0.0000} mm ({errUm:+0.0;-0.0;0.0} μm)";
                    errTb.Foreground = ErrBrush(err);
                }
                else
                {
                    refTb.Text = "—";
                    errTb.Text = "—";
                    errTb.Foreground = new SolidColorBrush(Color.FromRgb(94, 139, 170));
                }
            }
            else
            {
                measTb.Text = "N/A";
                refTb.Text = "—";
                errTb.Text = "—";
                errTb.Foreground = new SolidColorBrush(Color.FromRgb(94, 139, 170));
            }
        }

        private static string FormatErr(double v)
        {
            double um = v * 1000.0;
            return $"{v:+0.0000;-0.0000;0.0000} mm ({um:+0.0;-0.0;0.0} μm)";
        }

        private static SolidColorBrush ErrBrush(double err)
        {
            double abs = Math.Abs(err) * 1000.0;
            if (abs < 1.0) return new SolidColorBrush(Color.FromRgb(46, 204, 113));
            if (abs < 5.0) return new SolidColorBrush(Color.FromRgb(241, 196, 15));
            return new SolidColorBrush(Color.FromRgb(231, 76, 60));
        }

        // ════════════════════════════════════════════════════
        //  좌측 그래프: 회전 중심 좌표계 기준 포인트
        // ════════════════════════════════════════════════════

        private void GraphCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (IsLoaded) UpdateAll();
        }

        private void DrawGraph(AlignData data)
        {
            GraphCanvas.Children.Clear();

            double w = GraphCanvas.ActualWidth;
            double h = GraphCanvas.ActualHeight;
            if (w <= 0 || h <= 0) return;

            if (data == null)
            {
                AddNoData(w, h, "데이터 없음\n시퀀스를 실행하면 실시간으로 표시됩니다.");
                return;
            }

            // ── 포인트 수집 (있는 것만) ──
            var points = new List<(string name, double x, double y, Color color, bool isSquare)>();

            if (data.TL != null) points.Add(("TL", data.TL.X, data.TL.Y, Color.FromRgb(52, 152, 219), false));
            if (data.TR != null) points.Add(("TR", data.TR.X, data.TR.Y, Color.FromRgb(52, 152, 219), false));
            if (data.BL != null) points.Add(("BL", data.BL.X, data.BL.Y, Color.FromRgb(231, 76, 60), false));
            if (data.BR != null) points.Add(("BR", data.BR.X, data.BR.Y, Color.FromRgb(231, 76, 60), false));
            if (data.BFL != null) points.Add(("BFL", data.BFL.X, data.BFL.Y, Color.FromRgb(46, 204, 113), false));
            if (data.BFR != null) points.Add(("BFR", data.BFR.X, data.BFR.Y, Color.FromRgb(46, 204, 113), false));
            if (data.TCenter != null) points.Add(("TC", data.TCenter.X, data.TCenter.Y, Color.FromRgb(243, 156, 18), true));
            if (data.BCenter != null) points.Add(("BC", data.BCenter.X, data.BCenter.Y, Color.FromRgb(155, 89, 182), true));

            // 회전 중심 (원점) — 항상 표시
            points.Add(("HcRO", 0, 0, Colors.White, true));

            if (points.Count <= 1)
            {
                AddNoData(w, h, "좌표 데이터 대기 중...\n측정이 완료되면 점이 추가됩니다.");
                return;
            }

            // ── 범위 계산 ──
            double minX = points.Min(p => p.x);
            double maxX = points.Max(p => p.x);
            double minY = points.Min(p => p.y);
            double maxY = points.Max(p => p.y);

            double bufX = Math.Max((maxX - minX) * 0.35, 0.3);
            double bufY = Math.Max((maxY - minY) * 0.35, 0.3);
            minX -= bufX; maxX += bufX;
            minY -= bufY; maxY += bufY;

            double rangeX = maxX - minX;
            double rangeY = maxY - minY;

            const double pad = 55;
            double drawW = w - 2 * pad;
            double drawH = h - 2 * pad;
            double scale = Math.Min(drawW / rangeX, drawH / rangeY);

            double offsetX = pad + (drawW - rangeX * scale) / 2.0;
            double offsetY = pad + (drawH - rangeY * scale) / 2.0;

            double CX(double x) => offsetX + (x - minX) * scale;
            double CY(double y) => h - offsetY - (y - minY) * scale;

            // ── 격자선 ──
            var gridColor = Color.FromArgb(30, 150, 180, 220);
            const int div = 5;
            for (int i = 0; i <= div; i++)
            {
                double t = (double)i / div;

                double gx = minX + t * rangeX;
                AddLine(CX(gx), CY(minY), CX(gx), CY(maxY), gridColor, 1);
                AddText(CX(gx) - 16, CY(minY) + 4, gx.ToString("F3"),
                        Color.FromArgb(100, 140, 170, 200), 9);

                double gy = minY + t * rangeY;
                AddLine(CX(minX), CY(gy), CX(maxX), CY(gy), gridColor, 1);
                AddText(CX(minX) - 52, CY(gy) - 7, gy.ToString("F3"),
                        Color.FromArgb(100, 140, 170, 200), 9);
            }

            AddText(w / 2, CY(minY) + 18, "X (mm)", Color.FromArgb(160, 160, 190, 220), 10);
            AddText(CX(minX) - 52, pad - 16, "Y (mm)", Color.FromArgb(160, 160, 190, 220), 10);

            // ── 원점 십자선 ──
            if (0 >= minX && 0 <= maxX && 0 >= minY && 0 <= maxY)
            {
                var crossColor = Color.FromArgb(60, 255, 255, 255);
                AddLine(CX(0), CY(minY), CX(0), CY(maxY), crossColor, 1, isDashed: true);
                AddLine(CX(minX), CY(0), CX(maxX), CY(0), crossColor, 1, isDashed: true);
            }

            // ── 선분 그리기 (양쪽 점이 있을 때만) ──

            if (data.TL != null && data.TR != null)
            {
                AddLine(CX(data.TL.X), CY(data.TL.Y),
                        CX(data.TR.X), CY(data.TR.Y),
                        Color.FromArgb(180, 52, 152, 219), 2.0);

                if (data.TopAlignDist > 0)
                    AddDistLabel(CX((data.TL.X + data.TR.X) / 2),
                                 CY((data.TL.Y + data.TR.Y) / 2) - 16,
                                 data.TopAlignDist, Color.FromRgb(52, 152, 219));
            }

            if (data.BL != null && data.BR != null)
            {
                AddLine(CX(data.BL.X), CY(data.BL.Y),
                        CX(data.BR.X), CY(data.BR.Y),
                        Color.FromArgb(180, 231, 76, 60), 2.0);

                if (data.BtmAlignDist > 0)
                    AddDistLabel(CX((data.BL.X + data.BR.X) / 2),
                                 CY((data.BL.Y + data.BR.Y) / 2) + 6,
                                 data.BtmAlignDist, Color.FromRgb(231, 76, 60));
            }

            if (data.BFL != null && data.BFR != null)
            {
                AddLine(CX(data.BFL.X), CY(data.BFL.Y),
                        CX(data.BFR.X), CY(data.BFR.Y),
                        Color.FromArgb(140, 46, 204, 113), 1.5, isDashed: true);

                if (data.BtmFidDist > 0)
                    AddDistLabel(CX((data.BFL.X + data.BFR.X) / 2),
                                 CY((data.BFL.Y + data.BFR.Y) / 2) + 6,
                                 data.BtmFidDist, Color.FromRgb(46, 204, 113));
            }

            if (data.TCenter != null && data.BCenter != null)
            {
                AddLine(CX(data.TCenter.X), CY(data.TCenter.Y),
                        CX(data.BCenter.X), CY(data.BCenter.Y),
                        Color.FromArgb(80, 200, 200, 200), 1.2, isDashed: true);
            }

            // ── 포인트 그리기 ──
            const double r = 6;
            foreach (var (name, x, y, color, isSquare) in points)
            {
                double cx = CX(x);
                double cy = CY(y);

                if (isSquare)
                    AddSquare(cx, cy, r, color);
                else
                    AddPoint(cx, cy, r, color);

                string label = name == "HcRO" ? "O (HcRO)" : name;
                AddText(cx + r + 3, cy - 12, label, color, 11);
                AddText(cx + r + 3, cy + 1,
                        $"({x:F4}, {y:F4})",
                        Color.FromArgb(160, 160, 200, 230), 9);
            }

            // ── 포인트 개수 표시 ──
            int totalMarks = points.Count - 1; // HcRO 제외
            AddText(w - 120, 8, $"포인트: {totalMarks}/8",
                    Color.FromArgb(140, 160, 190, 220), 10);
        }

        // ── 선분 길이 라벨 ──
        private void AddDistLabel(double cx, double cy, double dist, Color color)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(200, 10, 22, 40)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(120, color.R, color.G, color.B)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(4, 1, 4, 1)
            };
            border.Child = new TextBlock
            {
                Text = $"{dist:F4} mm",
                Foreground = new SolidColorBrush(color),
                FontSize = 10,
                FontFamily = new FontFamily("Consolas")
            };
            Canvas.SetLeft(border, cx - 36);
            Canvas.SetTop(border, cy);
            GraphCanvas.Children.Add(border);
        }

        // ── 그래프 헬퍼 ──

        private void AddLine(double x1, double y1, double x2, double y2,
                             Color color, double thickness, bool isDashed = false)
        {
            var line = new Line
            {
                X1 = x1,
                Y1 = y1,
                X2 = x2,
                Y2 = y2,
                Stroke = new SolidColorBrush(color),
                StrokeThickness = thickness
            };
            if (isDashed) line.StrokeDashArray = new DoubleCollection { 4, 3 };
            GraphCanvas.Children.Add(line);
        }

        private void AddPoint(double cx, double cy, double radius, Color color)
        {
            var e = new Ellipse
            {
                Width = radius * 2,
                Height = radius * 2,
                Fill = new SolidColorBrush(color),
                Stroke = new SolidColorBrush(Colors.White),
                StrokeThickness = 1
            };
            Canvas.SetLeft(e, cx - radius);
            Canvas.SetTop(e, cy - radius);
            GraphCanvas.Children.Add(e);
        }

        private void AddSquare(double cx, double cy, double half, Color color)
        {
            var rect = new Rectangle
            {
                Width = half * 2,
                Height = half * 2,
                Fill = new SolidColorBrush(color),
                Stroke = new SolidColorBrush(Colors.White),
                StrokeThickness = 1
            };
            Canvas.SetLeft(rect, cx - half);
            Canvas.SetTop(rect, cy - half);
            GraphCanvas.Children.Add(rect);
        }

        private void AddText(double x, double y, string text, Color color, double fontSize)
        {
            var tb = new TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush(color),
                FontSize = fontSize
            };
            Canvas.SetLeft(tb, x);
            Canvas.SetTop(tb, y);
            GraphCanvas.Children.Add(tb);
        }

        private void AddNoData(double w, double h, string message)
        {
            var tb = new TextBlock
            {
                Text = message,
                Foreground = new SolidColorBrush(Color.FromArgb(180, 150, 170, 190)),
                FontSize = 14,
                TextAlignment = TextAlignment.Center
            };
            Canvas.SetLeft(tb, w / 2 - 140);
            Canvas.SetTop(tb, h / 2 - 20);
            GraphCanvas.Children.Add(tb);
        }
    }
}