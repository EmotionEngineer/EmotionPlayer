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
    public partial class ProgressBarWindow : Window
    {
        private void Progress_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }
        public ProgressBarWindow()
        {
            InitializeComponent();
        }
        public void UpdateProgress(int percentage)
        {
            pbLoad.Value = percentage;
        }
    }
}
