using System.Windows;
using System.Windows.Input;

namespace EmotionPlayer
{
    public partial class DarkMsgBox : Window
    {
        public DarkMsgBox(string rate, int pos, int neg)
        {
            InitializeComponent();
            Age.Text = string.IsNullOrWhiteSpace(rate) ? "N/A" : rate;
            Pos.Text = pos.ToString();
            Neg.Text = neg.ToString();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Close();
        }
    }
}