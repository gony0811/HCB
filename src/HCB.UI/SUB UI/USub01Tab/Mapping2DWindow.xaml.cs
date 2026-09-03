using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Telerik.Windows.Controls;

namespace HCB.UI
{
    /// <summary>
    /// 2D Mapping 전용 창. 타입(Grid / Wafer)에 따라 패널을 전환한다.
    ///  · Grid  : 기존 CalibrationTab의 2D Mapping 기능을 그대로 사용.
    ///  · Wafer : 웨이퍼 맵(사각형 그리드)을 생성·표시(그리드만; 마크는 표시하지 않음).
    /// DataContext = CalibrationTabViewModel (공유).
    /// </summary>
    public partial class Mapping2DWindow : RadWindow
    {
        private readonly CalibrationTabViewModel _vm;

        public Mapping2DWindow(CalibrationTabViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            DataContext = vm;

            // 맵이 재생성되면 다시 그림
            _vm.WaferMapChanged += DrawWaferMap;
            Closed += (_, _) => _vm.WaferMapChanged -= DrawWaferMap;
        }

        // 타입 토글 — 선택에 따라 Grid / Wafer 패널 전환
        private void MappingType_Checked(object sender, RoutedEventArgs e)
        {
            if (GridPanel == null || WaferPanel == null) return;   // InitializeComponent 이전 방지

            bool isGrid = GridTypeButton.IsChecked == true;
            GridPanel.Visibility = isGrid ? Visibility.Visible : Visibility.Collapsed;
            WaferPanel.Visibility = isGrid ? Visibility.Collapsed : Visibility.Visible;

            if (!isGrid) DrawWaferMap();   // Wafer 탭 표시 시 최신 상태로 그림
        }

        private void WaferCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => DrawWaferMap();

        // ── 전체 웨이퍼 맵 (셀 그리드만; 마크 미표시) ──
        //   그리기는 논리 격자(GridX/Y)만 사용한다 — mm(그리드 사이즈/간격/마크 피치) 미사용.
        //   셀·ID는 각각 하나의 Frozen Geometry(단일 Path)로 배치 렌더링한다.
        private void DrawWaferMap()
        {
            var canvas = WaferCanvas;
            if (canvas == null) return;
            canvas.Children.Clear();

            double w = canvas.ActualWidth;
            double h = canvas.ActualHeight;
            var cells = _vm.WaferCells;
            if (w <= 0 || h <= 0 || cells.Count == 0) return;

            // 단위 격자 범위로 뷰포트 맞춤 (셀 반 칸 = 0.5, 여백 90%)
            double extentX = 0.5, extentY = 0.5;
            foreach (var c in cells)
            {
                extentX = System.Math.Max(extentX, System.Math.Abs(c.GridX) + 0.5);
                extentY = System.Math.Max(extentY, System.Math.Abs(c.GridY) + 0.5);
            }

            double unit = System.Math.Min(w / (extentX * 2), h / (extentY * 2)) * 0.9; // px/단위칸
            double cx0 = w / 2.0;
            double cy0 = h / 2.0;
            double ToX(double gx) => cx0 + gx * unit;
            double ToY(double gy) => cy0 - gy * unit;   // 위쪽 +

            double sPix = unit * 0.9;   // 그리기용 셀 크기(칸의 90% → 시각적 간격)
            bool showId = sPix >= 22;   // 셀이 충분히 클 때만 ID 라벨 표시

            var cellGeo = new GeometryGroup();
            GeometryGroup? textGeo = showId ? new GeometryGroup() : null;
            var typeface = new Typeface("Segoe UI");
            double idFont = System.Math.Min(12, sPix * 0.28);
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            foreach (var cell in cells)
            {
                double left = ToX(cell.GridX) - sPix / 2.0;
                double top = ToY(cell.GridY) - sPix / 2.0;
                cellGeo.Children.Add(new RectangleGeometry(new Rect(left, top, sPix, sPix)));

                if (textGeo != null)
                {
                    var ft = new FormattedText(cell.Id.ToString(), CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight, typeface, idFont, Brushes.Black, dpi);
                    var tg = ft.BuildGeometry(new Point(
                        ToX(cell.GridX) - ft.Width / 2.0,
                        ToY(cell.GridY) - ft.Height / 2.0));
                    textGeo.Children.Add(tg);
                }
            }
            cellGeo.Freeze();

            var cellStroke = new SolidColorBrush(Color.FromArgb(180, 0x5E, 0x8B, 0xAA));
            cellStroke.Freeze();
            canvas.Children.Add(new Path
            {
                Data = cellGeo,
                Stroke = cellStroke,
                StrokeThickness = 1,
                IsHitTestVisible = false
            });

            if (textGeo != null)
            {
                textGeo.Freeze();
                var idBrush = new SolidColorBrush(Color.FromArgb(200, 0x8F, 0xB0, 0xC8));
                idBrush.Freeze();
                canvas.Children.Add(new Path
                {
                    Data = textGeo,
                    Fill = idBrush,
                    IsHitTestVisible = false
                });
            }
        }
    }
}
