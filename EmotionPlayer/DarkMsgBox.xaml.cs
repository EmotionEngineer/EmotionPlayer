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

namespace EmotionPlayer
{
    public partial class DarkMsgBox : Window
    {
        public DarkMsgBox(string rate, int pos, int neg)
        {
            InitializeComponent();
            this.Age.Text = rate.ToString();
            this.Pos.Text = pos.ToString();
            this.Neg.Text = neg.ToString();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            this.Close();
        }
    }
}
