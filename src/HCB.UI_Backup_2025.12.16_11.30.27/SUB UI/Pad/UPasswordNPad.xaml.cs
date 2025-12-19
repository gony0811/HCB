using System.Text;
using System.Windows;
using Telerik.Licensing.Model;
using Telerik.Windows.Controls;
using Button = System.Windows.Controls.Button;

namespace HCB.UI
{
    /// <summary>
    /// UPasswordNPad.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class UPasswordNPad : RadWindow
    {
        private readonly StringBuilder _buffer = new StringBuilder();
        public int MaxLength { get; set; } = 12;   // 필요 시 길이 제한
        public string Password => _buffer.ToString();

        

        public UPasswordNPad(Window owner, string title)
        {
            this.Owner = owner;
            this.Header = title;
            InitializeComponent();
        }

        private void RefreshTextBox()
        {
            tbInput.Text = new string('*', _buffer.Length);
        }

        private void OnDigitClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Content is string s && s.Length == 1 && char.IsDigit(s[0]))
            {
                if (_buffer.Length < MaxLength)
                {
                    _buffer.Append(s);
                    RefreshTextBox();
                }
            }
        }

        private void OnBackspace(object sender, RoutedEventArgs e)
        {
            if (_buffer.Length > 0)
            {
                _buffer.Length--;
                RefreshTextBox();
            }
        }

        private void OnClear(object sender, RoutedEventArgs e)
        {
            _buffer.Clear();
            RefreshTextBox();
        }

        private void OnEnter(object sender, RoutedEventArgs e)
        {
            // TODO: 필요 시 유효성 검사(길이/정책 등)
            DialogResult = true;   // 모달 종료 + true 반환
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;  // 모달 종료 + false 반환
        }
    }
}
