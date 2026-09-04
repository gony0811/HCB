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

        // 맵 좌표 변환(격자 → 픽셀). 클릭 히트테스트에 사용.
        private double _mapUnit, _mapCx0, _mapCy0;
        private bool _mapValid;

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

        // 셀 클릭 → 해당 셀 선택(고배 좌표 확인·이동 패널 표시)
        private void WaferCanvas_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (!_mapValid || _mapUnit <= 0) return;
            var p = e.GetPosition(WaferCanvas);
            double gx = (p.X - _mapCx0) / _mapUnit;
            double gy = (_mapCy0 - p.Y) / _mapUnit;   // 위쪽 +
            foreach (var cell in _vm.WaferCells)
                if (System.Math.Abs(gx - cell.GridX) <= 0.5 && System.Math.Abs(gy - cell.GridY) <= 0.5)
                {
                    _vm.SelectCell(cell);
                    return;
                }
        }

        // ── 전체 웨이퍼 맵 (셀 그리드만; 마크 미표시) ──
        //   그리기는 논리 격자(GridX/Y)만 사용한다 — mm(그리드 사이즈/간격/마크 피치) 미사용.
        //   셀·ID는 각각 하나의 Frozen Geometry(단일 Path)로 배치 렌더링한다.
        private void DrawWaferMap()
        {
            _mapValid = false;
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

            // 클릭 히트테스트용 변환 저장
            _mapUnit = unit; _mapCx0 = cx0; _mapCy0 = cy0; _mapValid = true;

            double sPix = unit * 0.9;   // 그리기용 셀 크기(칸의 90% → 시각적 간격)
            bool showId = sPix >= 22;   // 셀이 충분히 클 때만 ID 라벨 표시

            // 상태별 채움 버킷(배치 렌더) + 전체 외곽선 + ID 텍스트
            var gStroke = new GeometryGroup();          // 모든 셀 외곽선
            var gMeasured = new GeometryGroup();        // 완료
            var gMeasuring = new GeometryGroup();       // 측정중
            var gSelected = new GeometryGroup();        // 선택(클릭)
            GeometryGroup? textGeo = showId ? new GeometryGroup() : null;
            var typeface = new Typeface("Segoe UI");
            double idFont = System.Math.Min(12, sPix * 0.28);
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            foreach (var cell in cells)
            {
                double left = ToX(cell.GridX) - sPix / 2.0;
                double top = ToY(cell.GridY) - sPix / 2.0;
                var rect = new RectangleGeometry(new Rect(left, top, sPix, sPix));
                gStroke.Children.Add(rect);

                if (ReferenceEquals(cell, _vm.SelectedCell)) gSelected.Children.Add(rect);
                if (cell.State == CellMeasureState.Measured) gMeasured.Children.Add(rect);
                else if (cell.State == CellMeasureState.Measuring) gMeasuring.Children.Add(rect);

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

            // 채움(상태별) — 외곽선보다 먼저
            AddFill(canvas, gMeasured, Color.FromArgb(150, 0x2E, 0xCC, 0x71));  // 완료: 초록
            AddFill(canvas, gMeasuring, Color.FromArgb(170, 0xE6, 0x7E, 0x22)); // 측정중: 주황
            AddFill(canvas, gSelected, Color.FromArgb(150, 0x34, 0x98, 0xDB));  // 선택: 파랑

            // 전체 외곽선
            gStroke.Freeze();
            var cellStroke = new SolidColorBrush(Color.FromArgb(180, 0x5E, 0x8B, 0xAA));
            cellStroke.Freeze();
            canvas.Children.Add(new Path
            {
                Data = gStroke,
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

        // 상태별 채움 Path 추가(비어있으면 생략).
        private static void AddFill(Canvas canvas, GeometryGroup geo, Color color)
        {
            if (geo.Children.Count == 0) return;
            geo.Freeze();
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            canvas.Children.Add(new Path { Data = geo, Fill = brush, IsHitTestVisible = false });
        }
    }
}
