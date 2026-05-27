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
    public partial class AlignResultWindow : RadWindow
    {
        private readonly AlignData _data;
        private readonly double _refAlignDist; // 설계 기준 Align 선분 길이 (mm)
        private readonly double _refFidDist;   // 설계 기준 Fid 선분 길이 (mm)

        public AlignResultWindow(AlignData data,
                                 double refAlignDist = double.NaN,
                                 double refFidDist = double.NaN)
        {
            InitializeComponent();
            _data = data;
            _refAlignDist = refAlignDist;
            _refFidDist = refFidDist;

            Header = "정렬 결과 — 회전 중심 좌표계";
            Loaded += (s, e) => UpdateAll();
        }

        private void UpdateAll()
        {
            UpdateDistancePanel();
            DrawGraph();
        }

        // ════════════════════════════════════════════════════
        //  우측 패널: 선분 길이 & 오차
        // ════════════════════════════════════════════════════

        private void UpdateDistancePanel()
        {
            if (_data == null) return;

            // ── 개별 항목 ──
            SetDistRow(TopAlignDistText, TopAlignRefText, TopAlignErrText,
                       _data.TopAlignDist, _refAlignDist);
            SetDistRow(BtmAlignDistText, BtmAlignRefText, BtmAlignErrText,
                       _data.BtmAlignDist, _refAlignDist);
            SetDistRow(TopFidDistText, TopFidRefText, TopFidErrText,
                       _data.TopFidDist, _refFidDist);
            SetDistRow(BtmFidDistText, BtmFidRefText, BtmFidErrText,
                       _data.BtmFidDist, _refFidDist);

            // ── 오차 요약 ──
            // Top: Fid − Align 길이 차이
            if (_data.TopFidDist > 0 && _data.TopAlignDist > 0)
            {
                double d = _data.TopFidDist - _data.TopAlignDist;
                TopDeltaText.Text = FormatErr(d);
                TopDeltaText.Foreground = ErrBrush(d);
            }
            else TopDeltaText.Text = "N/A";

            // Btm: Fid − Align 길이 차이
            if (_data.BtmFidDist > 0 && _data.BtmAlignDist > 0)
            {
                double d = _data.BtmFidDist - _data.BtmAlignDist;
                BtmDeltaText.Text = FormatErr(d);
                BtmDeltaText.Foreground = ErrBrush(d);
            }
            else BtmDeltaText.Text = "N/A";

            // Top Align − Btm Align 길이 차이
            if (_data.TopAlignDist > 0 && _data.BtmAlignDist > 0)
            {
                double d = _data.TopAlignDist - _data.BtmAlignDist;
                AlignDiffText.Text = FormatErr(d);
                AlignDiffText.Foreground = ErrBrush(d);
            }
            else AlignDiffText.Text = "N/A";

            // ── 보정 결과 ──
            ResultXText.Text = _data.ResultX.ToString("+0.0000;-0.0000;0.0000") + " mm";
            ResultYText.Text = _data.ResultY.ToString("+0.0000;-0.0000;0.0000") + " mm";
            ResultTText.Text = _data.ResultT.ToString("+0.0000;-0.0000;0.0000") + " °";
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
                    double errUm = err * 1000.0; // mm → μm
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
            double abs = Math.Abs(err) * 1000.0; // μm
            if (abs < 1.0) return new SolidColorBrush(Color.FromRgb(46, 204, 113));  // 초록
            if (abs < 5.0) return new SolidColorBrush(Color.FromRgb(241, 196, 15));  // 노랑
            return new SolidColorBrush(Color.FromRgb(231, 76, 60));                   // 빨강
        }

        // ════════════════════════════════════════════════════
        //  좌측 그래프: 회전 중심 좌표계 기준 포인트
        // ════════════════════════════════════════════════════

        private void GraphCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (IsLoaded) DrawGraph();
        }

        private void DrawGraph()
        {
            GraphCanvas.Children.Clear();

            double w = GraphCanvas.ActualWidth;
            double h = GraphCanvas.ActualHeight;
            if (w <= 0 || h <= 0) return;

            if (_data == null)
            {
                AddNoData(w, h);
                return;
            }

            // ── 포인트 수집 ──
            var points = new List<(string name, double x, double y, Color color, bool isSquare)>();

            if (_data.TL != null) points.Add(("TL", _data.TL.X, _data.TL.Y, Color.FromRgb(52, 152, 219), false));
            if (_data.TR != null) points.Add(("TR", _data.TR.X, _data.TR.Y, Color.FromRgb(52, 152, 219), false));
            if (_data.BL != null) points.Add(("BL", _data.BL.X, _data.BL.Y, Color.FromRgb(231, 76, 60), false));
            if (_data.BR != null) points.Add(("BR", _data.BR.X, _data.BR.Y, Color.FromRgb(231, 76, 60), false));
            if (_data.BFL != null) points.Add(("BFL", _data.BFL.X, _data.BFL.Y, Color.FromRgb(46, 204, 113), false));
            if (_data.BFR != null) points.Add(("BFR", _data.BFR.X, _data.BFR.Y, Color.FromRgb(46, 204, 113), false));
            if (_data.TCenter != null) points.Add(("TC", _data.TCenter.X, _data.TCenter.Y, Color.FromRgb(243, 156, 18), true));
            if (_data.BCenter != null) points.Add(("BC", _data.BCenter.X, _data.BCenter.Y, Color.FromRgb(155, 89, 182), true));

            // 회전 중심 (원점)
            points.Add(("HcRO", 0, 0, Colors.White, true));

            if (points.Count <= 1)
            {
                AddNoData(w, h);
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

            // 축 레이블
            AddText(w / 2, CY(minY) + 18, "X (mm)", Color.FromArgb(160, 160, 190, 220), 10);
            AddText(CX(minX) - 52, pad - 16, "Y (mm)", Color.FromArgb(160, 160, 190, 220), 10);

            // ── 원점 십자선 (HcRO가 범위 안에 있을 때) ──
            if (0 >= minX && 0 <= maxX && 0 >= minY && 0 <= maxY)
            {
                var crossColor = Color.FromArgb(60, 255, 255, 255);
                AddLine(CX(0), CY(minY), CX(0), CY(maxY), crossColor, 1, isDashed: true);
                AddLine(CX(minX), CY(0), CX(maxX), CY(0), crossColor, 1, isDashed: true);
            }

            // ── 선분 그리기 ──

            // Top Align 선분 (TL → TR)
            if (_data.TL != null && _data.TR != null)
            {
                AddLine(CX(_data.TL.X), CY(_data.TL.Y),
                        CX(_data.TR.X), CY(_data.TR.Y),
                        Color.FromArgb(180, 52, 152, 219), 2.0);

                // 길이 라벨
                if (_data.TopAlignDist > 0)
                    AddDistLabel(CX((_data.TL.X + _data.TR.X) / 2),
                                 CY((_data.TL.Y + _data.TR.Y) / 2) - 16,
                                 _data.TopAlignDist, Color.FromRgb(52, 152, 219));
            }

            // Btm Align 선분 (BL → BR)
            if (_data.BL != null && _data.BR != null)
            {
                AddLine(CX(_data.BL.X), CY(_data.BL.Y),
                        CX(_data.BR.X), CY(_data.BR.Y),
                        Color.FromArgb(180, 231, 76, 60), 2.0);

                if (_data.BtmAlignDist > 0)
                    AddDistLabel(CX((_data.BL.X + _data.BR.X) / 2),
                                 CY((_data.BL.Y + _data.BR.Y) / 2) + 6,
                                 _data.BtmAlignDist, Color.FromRgb(231, 76, 60));
            }

            // Btm Fid 선분 (BFL → BFR) — 점선
            if (_data.BFL != null && _data.BFR != null)
            {
                AddLine(CX(_data.BFL.X), CY(_data.BFL.Y),
                        CX(_data.BFR.X), CY(_data.BFR.Y),
                        Color.FromArgb(140, 46, 204, 113), 1.5, isDashed: true);

                if (_data.BtmFidDist > 0)
                    AddDistLabel(CX((_data.BFL.X + _data.BFR.X) / 2),
                                 CY((_data.BFL.Y + _data.BFR.Y) / 2) + 6,
                                 _data.BtmFidDist, Color.FromRgb(46, 204, 113));
            }

            // TCenter → BCenter 연결 (점선)
            if (_data.TCenter != null && _data.BCenter != null)
            {
                AddLine(CX(_data.TCenter.X), CY(_data.TCenter.Y),
                        CX(_data.BCenter.X), CY(_data.BCenter.Y),
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

                // 레이블 (HcRO는 특별 표기)
                string label = name == "HcRO" ? "O (HcRO)" : name;
                AddText(cx + r + 3, cy - 12, label, color, 11);
                AddText(cx + r + 3, cy + 1,
                        $"({x:F4}, {y:F4})",
                        Color.FromArgb(160, 160, 200, 230), 9);
            }
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
                X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
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
                Width = radius * 2, Height = radius * 2,
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
                Width = half * 2, Height = half * 2,
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

        private void AddNoData(double w, double h)
        {
            var tb = new TextBlock
            {
                Text = "데이터 없음\n보정(TopPlace)을 먼저 수행하세요.",
                Foreground = new SolidColorBrush(Color.FromArgb(180, 150, 170, 190)),
                FontSize = 14,
                TextAlignment = TextAlignment.Center
            };
            Canvas.SetLeft(tb, w / 2 - 120);
            Canvas.SetTop(tb, h / 2 - 20);
            GraphCanvas.Children.Add(tb);
        }
    }
}
