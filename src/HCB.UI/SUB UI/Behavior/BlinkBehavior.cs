using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace HCB.UI
{
    public static class BlinkBehavior
    {
        // 깜박임 상태를 제어하는 연결 속성 (Attached Property)
        public static readonly DependencyProperty IsBlinkingProperty =
            DependencyProperty.RegisterAttached(
                "IsBlinking",
                typeof(bool),
                typeof(BlinkBehavior),
                new PropertyMetadata(false, OnIsBlinkingChanged));

        public static bool GetIsBlinking(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsBlinkingProperty);
        }

        public static void SetIsBlinking(DependencyObject obj, bool value)
        {
            obj.SetValue(IsBlinkingProperty, value);
        }

        // Storyboard를 저장하기 위한 연결 속성
        private static readonly DependencyProperty BlinkingStoryboardProperty =
            DependencyProperty.RegisterAttached(
                "BlinkingStoryboard",
                typeof(Storyboard),
                typeof(BlinkBehavior),
                new PropertyMetadata(null));

        private static Storyboard GetBlinkingStoryboard(DependencyObject obj)
        {
            return (Storyboard)obj.GetValue(BlinkingStoryboardProperty);
        }

        private static void SetBlinkingStoryboard(DependencyObject obj, Storyboard value)
        {
            obj.SetValue(BlinkingStoryboardProperty, value);
        }

        // IsBlinking 속성이 변경될 때 호출되는 콜백 메서드
        private static void OnIsBlinkingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not Control control) return;

            bool isBlinking = (bool)e.NewValue;

            if (isBlinking)
            {
                var storyboard = GetBlinkingStoryboard(control);
                if (storyboard == null)
                {
                    storyboard = new Storyboard();
                    var animation = new DoubleAnimation
                    {
                        From = 1.0,
                        To = 0.3,
                        Duration = new Duration(TimeSpan.FromSeconds(0.5)),
                        AutoReverse = true,
                        RepeatBehavior = RepeatBehavior.Forever
                    };
                    Storyboard.SetTarget(animation, control);
                    Storyboard.SetTargetProperty(animation, new PropertyPath(UIElement.OpacityProperty));
                    storyboard.Children.Add(animation);
                    SetBlinkingStoryboard(control, storyboard);
                }
                storyboard.Begin();
            }
            else
            {
                GetBlinkingStoryboard(control)?.Stop();
                // Opacity를 원래 값으로 복원
                control.BeginAnimation(UIElement.OpacityProperty, null);
            }
        }
    }
}
