using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace EmotionPlayer.Controls
{
    public partial class PathButton : Button
    {
        public static readonly DependencyProperty DataProperty;
        public static readonly DependencyProperty NormalBrushProperty;
        public static readonly DependencyProperty OverBrushProperty;
        public static readonly DependencyProperty PressedBrushProperty;
        public static readonly DependencyProperty StretchProperty;

        public Geometry Data
        {
            get => GetValue(DataProperty) as Geometry;
            set => SetValue(DataProperty, value);
        }
        public Stretch Stretch
        {
            get => (Stretch)(GetValue(StretchProperty) ?? Stretch.None);
            set => SetValue(StretchProperty, value);
        }
        public SolidColorBrush NormalBrush
        {
            get => GetValue(NormalBrushProperty) as SolidColorBrush;
            set => SetValue(NormalBrushProperty, value);
        }
        public SolidColorBrush OverBrush
        {
            get => GetValue(OverBrushProperty) as SolidColorBrush;
            set => SetValue(OverBrushProperty, value);
        }
        public SolidColorBrush PressedBrush
        {
            get => GetValue(PressedBrushProperty) as SolidColorBrush;
            set => SetValue(PressedBrushProperty, value);
        }

        static PathButton()
        {
            DataProperty =
                DependencyProperty.Register("Data", typeof(Geometry), typeof(PathButton));

            NormalBrushProperty =
                DependencyProperty.Register("NormalBrush", typeof(SolidColorBrush), typeof(PathButton));

            OverBrushProperty =
                DependencyProperty.Register("OverBrush", typeof(SolidColorBrush), typeof(PathButton));

            PressedBrushProperty =
               DependencyProperty.Register("PressedBrush", typeof(SolidColorBrush), typeof(PathButton));

            StretchProperty = 
                DependencyProperty.Register("Stretch", typeof(Stretch), typeof(PathButton));
        }

        public PathButton()
        {
            Stretch = Stretch.Uniform;

            InitializeComponent();
        }

        private void Button_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            Foreground = PressedBrush;
        }
        private void Button_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            Foreground = IsMouseOver ? OverBrush : NormalBrush;
        }
        private void Button_MouseEnter(object sender, MouseEventArgs e)
        {
            Foreground = IsPressed ? PressedBrush : OverBrush;
        }
        private void Button_MouseLeave(object sender, MouseEventArgs e)
        {
            Foreground = IsPressed ? OverBrush : NormalBrush;
        }
        private void Button_Loaded(object sender, RoutedEventArgs e)
        {
            Foreground = IsMouseOver ? OverBrush : NormalBrush;
        }
    }
}
