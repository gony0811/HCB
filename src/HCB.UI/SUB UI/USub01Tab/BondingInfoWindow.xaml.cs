using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Telerik.Windows.Controls;
using HCB.Data.Entity.Type;
using ValueType = HCB.Data.Entity.Type.ValueType;

namespace HCB.UI
{
    // ── 본딩 데이터 포인트 ──────────────────────────────────────────
    public class BondingDataPoint
    {
        public double TimeS  { get; set; } // 경과 시간 (초)
        public double ForceN { get; set; } // 측정 Force (N)
    }

    public partial class BondingInfoWindow : RadWindow
    {
        // Recipe 파라미터 키 이름
        public const string PARAM_FORCE = "BondingForce";
        public const string PARAM_TIME  = "BondingTime";

        private readonly RecipeService _recipeService;
        private readonly IReadOnlyList<BondingDataPoint> _history;

        private double _forceN  = 50.0;  // 기본값 50 N
        private double _timeS   = 5.0;   // 기본값 5 s
        private bool   _inputValid = true;

        public BondingInfoWindow(RecipeService recipeService, IReadOnlyList<BondingDataPoint> history)
        {
            InitializeComponent();
            _recipeService = recipeService;
            _history       = history ?? Array.Empty<BondingDataPoint>();

            Header = "Bonding 공정 정보";
            Loaded += (s, e) => { LoadFromRecipe(); DrawGraph(); };
        }

        // ── Recipe에서 초기값 로드 ─────────────────────────────────
        private void LoadFromRecipe()
        {
            var recipe = _recipeService?.UseRecipe;
            RecipeNameText.Text = recipe?.Name ?? "(선택된 레시피 없음)";

            if (recipe != null)
            {
                var forceParam = recipe.ParamList.FirstOrDefault(p => p.Name == PARAM_FORCE);
                var timeParam  = recipe.ParamList.FirstOrDefault(p => p.Name == PARAM_TIME);

                if (forceParam != null && double.TryParse(forceParam.Value, out double f)) _forceN = f;
                if (timeParam  != null && double.TryParse(timeParam.Value,  out double t)) _timeS  = t;

                // 범위 표시
                ForceRangeText.Text = FormatRange(forceParam, "N");
                TimeRangeText.Text  = FormatRange(timeParam,  "s");
            }

            ForceTextBox.Text = _forceN.ToString("F3");
            TimeTextBox.Text  = _timeS.ToString("F3");
            UpdateEndTimeDisplay();
        }

        private static string FormatRange(RecipeParamDto param, string unit)
        {
            if (param == null) return "—";
            bool hasMin = !string.IsNullOrEmpty(param.Minimum);
            bool hasMax = !string.IsNullOrEmpty(param.Maximum);
            if (!hasMin && !hasMax) return "—";
            return $"{(hasMin ? param.Minimum : "—")} ~ {(hasMax ? param.Maximum : "—")} {unit}";
        }

        private void UpdateEndTimeDisplay()
        {
            EndTimeDisplay.Text = _timeS.ToString("F3");
        }

        // ── 입력 이벤트 ────────────────────────────────────────────
        private void ForceTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (double.TryParse(ForceTextBox.Text, out double v) && v > 0)
            {
                _forceN = v;
                ForceTextBox.BorderBrush = new SolidColorBrush(Color.FromRgb(42, 90, 140));
                _inputValid = true;
                if (IsLoaded) DrawGraph();
            }
            else
            {
                ForceTextBox.BorderBrush = new SolidColorBrush(Colors.Red);
                _inputValid = false;
            }
        }

        private void TimeTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (double.TryParse(TimeTextBox.Text, out double v) && v > 0)
            {
                _timeS = v;
                TimeTextBox.BorderBrush = new SolidColorBrush(Color.FromRgb(42, 90, 140));
                _inputValid = true;
                UpdateEndTimeDisplay();
                if (IsLoaded) DrawGraph();
            }
            else
            {
                TimeTextBox.BorderBrush = new SolidColorBrush(Colors.Red);
                _inputValid = false;
            }
        }

        // ── Recipe 저장 ────────────────────────────────────────────
        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_inputValid)
            {
                ShowStatus("입력값을 확인해주세요.", isError: true);
                return;
            }

            var recipe = _recipeService?.UseRecipe;
            if (recipe == null)
            {
                ShowStatus("활성 Recipe가 없습니다.", isError: true);
                return;
            }

            SaveButton.IsEnabled = false;
            ShowStatus("저장 중...", isError: false);

            try
            {
                // 메인 Dispatcher에서 DB 작업 수행
                await Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    await SaveParam(recipe, PARAM_FORCE, _forceN.ToString("F4"),
                        ValueType.Double, "BondingForce (N)");
                    await SaveParam(recipe, PARAM_TIME,  _timeS.ToString("F4"),
                        ValueType.Double, "BondingTime (s)");
                });

                ShowStatus($"저장 완료\nForce: {_forceN:F3} N  /  Time: {_timeS:F3} s", isError: false);
            }
            catch (Exception ex)
            {
                ShowStatus($"저장 실패: {ex.Message}", isError: true);
            }
            finally
            {
                SaveButton.IsEnabled = true;
            }
        }

        private async System.Threading.Tasks.Task SaveParam(
            RecipeDto recipe, string name, string value, ValueType vType, string description)
        {
            var existing = recipe.ParamList.FirstOrDefault(p => p.Name == name);
            if (existing != null)
            {
                existing.Value = value;
                await _recipeService.UpdateRecipeParam(existing);
            }
            else
            {
                var newParam = new RecipeParamDto
                {
                    RecipeId    = recipe.Id,
                    Name        = name,
                    Value       = value,
                    ValueType   = vType,
                    UnitType    = UnitType.None,
                    Description = description
                };
                await _recipeService.AddRecipeParam(newParam);
            }
        }

        private void ShowStatus(string message, bool isError)
        {
            StatusText.Text      = message;
            StatusText.Foreground = new SolidColorBrush(isError ? Colors.Salmon : Colors.LightGreen);
            StatusBorder.Visibility = Visibility.Visible;
        }

        // ── 그래프 ─────────────────────────────────────────────────
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

            // ── 축 범위 ─────────────────────────────────────────────
            double maxT = _timeS  * 1.35;
            double maxF = _forceN * 1.35;

            // 측정 데이터가 있으면 범위 확장
            if (_history.Count > 0)
            {
                maxT = Math.Max(maxT, _history.Max(p => p.TimeS)  * 1.15);
                maxF = Math.Max(maxF, _history.Max(p => p.ForceN) * 1.15);
            }

            const double padL = 62; // left (Y-axis labels)
            const double padR = 20;
            const double padT = 28;
            const double padB = 48; // bottom (X-axis labels)

            double plotW = w - padL - padR;
            double plotH = h - padT - padB;

            double ToCanvasX(double t) => padL + t / maxT * plotW;
            double ToCanvasY(double f) => h - padB - f / maxF * plotH;

            // ── 격자선 ──────────────────────────────────────────────
            var gridColor = Color.FromArgb(35, 150, 180, 220);
            const int gridDiv = 5;
            for (int i = 0; i <= gridDiv; i++)
            {
                double tf = (double)i / gridDiv;

                // 수직
                double gx = ToCanvasX(tf * maxT);
                AddLine(gx, padT, gx, h - padB, gridColor, 1);
                double tLabel = tf * maxT;
                AddText(gx - 14, h - padB + 6, tLabel.ToString("F2"),
                        Color.FromArgb(140, 150, 180, 200), 9);

                // 수평
                double gy = ToCanvasY(tf * maxF);
                AddLine(padL, gy, w - padR, gy, gridColor, 1);
                double fLabel = tf * maxF;
                AddText(2, gy - 7, fLabel.ToString("F1"),
                        Color.FromArgb(140, 150, 180, 200), 9);
            }

            // ── 축 ─────────────────────────────────────────────────
            var axisColor = Color.FromArgb(180, 100, 140, 180);
            AddLine(padL, padT, padL, h - padB, axisColor, 1.5);     // Y축
            AddLine(padL, h - padB, w - padR, h - padB, axisColor, 1.5); // X축

            // 축 레이블
            AddText(padL + plotW / 2 - 10, h - padB + 18, "Time (s)",
                    Color.FromArgb(160, 160, 190, 220), 11);
            AddRotatedYLabel("Force (N)", padL, padT, plotH);

            // ── Force 수평 기준선 (점선) ───────────────────────────
            double lineY = ToCanvasY(_forceN);
            AddDashedLine(padL, lineY, w - padR, lineY,
                          Color.FromArgb(120, 52, 152, 219), 1);
            AddText(w - padR + 2, lineY - 7, $"{_forceN:F1}N",
                    Color.FromArgb(180, 52, 152, 219), 9);

            // ── 설정 Force 프로파일 (채움 + 외곽선) ──────────────────
            double cx0  = ToCanvasX(0);
            double cxT  = ToCanvasX(_timeS);
            double cyF  = ToCanvasY(_forceN);
            double cy0  = ToCanvasY(0);

            // 채움 (반투명)
            var fillGeo = new PathGeometry();
            var fillFig = new PathFigure { StartPoint = new Point(cx0, cy0), IsClosed = true };
            fillFig.Segments.Add(new LineSegment(new Point(cx0, cyF),   true));
            fillFig.Segments.Add(new LineSegment(new Point(cxT, cyF),   true));
            fillFig.Segments.Add(new LineSegment(new Point(cxT, cy0),   true));
            fillGeo.Figures.Add(fillFig);
            GraphCanvas.Children.Add(new System.Windows.Shapes.Path
            {
                Data            = fillGeo,
                Fill            = new SolidColorBrush(Color.FromArgb(40, 52, 152, 219)),
                Stroke          = Brushes.Transparent,
                StrokeThickness = 0
            });

            // 외곽선
            var outlineGeo = new PathGeometry();
            var outlineFig = new PathFigure { StartPoint = new Point(cx0, cy0), IsClosed = false };
            outlineFig.Segments.Add(new LineSegment(new Point(cx0, cyF), true));
            outlineFig.Segments.Add(new LineSegment(new Point(cxT, cyF), true));
            outlineFig.Segments.Add(new LineSegment(new Point(cxT, cy0), true));
            outlineGeo.Figures.Add(outlineFig);
            GraphCanvas.Children.Add(new System.Windows.Shapes.Path
            {
                Data            = outlineGeo,
                Stroke          = new SolidColorBrush(Color.FromArgb(230, 52, 152, 219)),
                StrokeThickness = 2.5,
                Fill            = Brushes.Transparent
            });

            // ── 종료 시점 수직선 (점선) ───────────────────────────
            AddDashedLine(cxT, padT, cxT, h - padB,
                          Color.FromArgb(220, 241, 196, 15), 2);

            // 종료 시점 레이블
            var endBorder = new Border
            {
                Background      = new SolidColorBrush(Color.FromArgb(200, 30, 50, 20)),
                BorderBrush     = new SolidColorBrush(Color.FromArgb(220, 241, 196, 15)),
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(3),
                Padding         = new Thickness(5, 2, 5, 2)
            };
            endBorder.Child = new TextBlock
            {
                Text       = $"T = {_timeS:F3} s",
                Foreground = new SolidColorBrush(Color.FromArgb(230, 241, 196, 15)),
                FontSize   = 10,
                FontFamily = new FontFamily("Consolas")
            };
            Canvas.SetLeft(endBorder, cxT - 38);
            Canvas.SetTop(endBorder,  padT - 22);
            GraphCanvas.Children.Add(endBorder);

            // ── 측정 데이터 오버레이 (빨간 폴리라인) ─────────────────
            if (_history.Count >= 2)
            {
                var pts = new PointCollection(
                    _history.Select(p => new Point(ToCanvasX(p.TimeS), ToCanvasY(p.ForceN))));
                GraphCanvas.Children.Add(new Polyline
                {
                    Points          = pts,
                    Stroke          = new SolidColorBrush(Color.FromArgb(220, 231, 76, 60)),
                    StrokeThickness = 2,
                    Fill            = Brushes.Transparent
                });
                // 최대 Force 표시
                double peakF = _history.Max(p => p.ForceN);
                double peakT = _history.First(p => p.ForceN == peakF).TimeS;
                AddText(ToCanvasX(peakT) + 4, ToCanvasY(peakF) - 16,
                        $"Peak: {peakF:F2} N",
                        Color.FromArgb(220, 231, 76, 60), 10);
            }
            else if (_history.Count == 1)
            {
                var pt = _history[0];
                AddPoint(ToCanvasX(pt.TimeS), ToCanvasY(pt.ForceN), 5, Colors.OrangeRed);
            }

            // ── 현재 Force 포인트 표시 (설정값) ──────────────────────
            AddPoint(cxT, cyF, 6, Color.FromRgb(52, 152, 219));
            AddText(cxT + 8, cyF - 9, $"{_forceN:F2} N  @  {_timeS:F3} s",
                    Color.FromArgb(200, 52, 152, 219), 10);
        }

        // ── 헬퍼 ─────────────────────────────────────────────────────
        private void AddLine(double x1, double y1, double x2, double y2,
                             Color color, double thickness)
        {
            GraphCanvas.Children.Add(new Line
            {
                X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                Stroke          = new SolidColorBrush(color),
                StrokeThickness = thickness
            });
        }

        private void AddDashedLine(double x1, double y1, double x2, double y2,
                                   Color color, double thickness)
        {
            GraphCanvas.Children.Add(new Line
            {
                X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                Stroke          = new SolidColorBrush(color),
                StrokeThickness = thickness,
                StrokeDashArray = new DoubleCollection { 5, 3 }
            });
        }

        private void AddPoint(double cx, double cy, double r, Color color)
        {
            var e = new Ellipse
            {
                Width  = r * 2, Height = r * 2,
                Fill   = new SolidColorBrush(color),
                Stroke = new SolidColorBrush(Colors.White),
                StrokeThickness = 1
            };
            Canvas.SetLeft(e, cx - r); Canvas.SetTop(e, cy - r);
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
            Canvas.SetLeft(tb, x); Canvas.SetTop(tb, y);
            GraphCanvas.Children.Add(tb);
        }

        private void AddRotatedYLabel(string text, double padL, double padT, double plotH)
        {
            var tb = new TextBlock
            {
                Text            = text,
                Foreground      = new SolidColorBrush(Color.FromArgb(160, 160, 190, 220)),
                FontSize        = 11,
                RenderTransform = new RotateTransform(-90)
            };
            Canvas.SetLeft(tb, 2);
            Canvas.SetTop(tb,  padT + plotH / 2 + 30);
            GraphCanvas.Children.Add(tb);
        }
    }
}
