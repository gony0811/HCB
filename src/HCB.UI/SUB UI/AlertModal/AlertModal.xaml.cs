using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace HCB.UI
{
    /// <summary>
    /// AlertModal.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class AlertModal : Window
    {
        public AlertModal()
        {
            InitializeComponent();
        }
        // 제목 텍스트
        public string TitleText
        {
            get => (string)GetValue(TitleTextProperty);
            set => SetValue(TitleTextProperty, value);
        }
        public static readonly DependencyProperty TitleTextProperty =
            DependencyProperty.Register(nameof(TitleText), typeof(string), typeof(AlertModal),
                new PropertyMetadata("확인"));

        // 메시지 텍스트
        public string MessageText
        {
            get => (string)GetValue(MessageTextProperty);
            set => SetValue(MessageTextProperty, value);
        }
        public static readonly DependencyProperty MessageTextProperty =
            DependencyProperty.Register(nameof(MessageText), typeof(string), typeof(AlertModal),
                new PropertyMetadata(""));

        private void OnOkClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true; // 닫히면서 true 반환
        }

        public static bool Ask(Window owner, string title, string message)
        {
            var w = new AlertModal
            {
                Owner = owner,
                TitleText = title,
                Title = title,      // 윈도우 시스템 타이틀도 동일하게
                MessageText = message
            };
            var result = w.ShowDialog();
            return result == true;
        }
    }
}
