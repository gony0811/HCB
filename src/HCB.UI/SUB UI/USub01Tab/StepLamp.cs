using System.Windows;
using System.Windows.Controls;

namespace HCB.UI
{
    // 재사용 가능한 램프 컨트롤: State에 바인딩하면 스타일 트리거가 동작합니다.
    public class StepLamp : ContentControl
    {
        static StepLamp()
        {
            // 스타일을 명시적으로 사용할 경우 필요 없으나 안전하게 등록
            DefaultStyleKeyProperty.OverrideMetadata(typeof(StepLamp), new FrameworkPropertyMetadata(typeof(StepLamp)));
        }

        // State: 외부에서 바인딩할 속성 (enum/string/boolean 등 어떤 타입도 가능)
        public static readonly DependencyProperty StateProperty =
            DependencyProperty.Register(nameof(State), typeof(object), typeof(StepLamp), new PropertyMetadata(null));

        public object State
        {
            get => GetValue(StateProperty);
            set => SetValue(StateProperty, value);
        }
    }
}