using System;
using System.Windows;
using System.Windows.Data;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Threading;
using System.Windows.Threading;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Imaging;

namespace EmotionPlayer
{
    public partial class MainWindow
    {
        private Thread scrollUpdater, cursorUpdater;
        private bool isSliderCatched = false;
        private bool isVolSliderCatched = false;
        private bool isSliderKeyCatch = false;


        public void Skip(int delta)
        {
            if (!isSliderKeyCatch)
            {
                UpdateTimeLine();
                isSliderKeyCatch = true;
            }
            slider.Value += delta;
        }
        public void UpdateTimeLine()
        {
            if (!isSliderCatched && !isSliderKeyCatch)
                slider.Value = mediaElement.Position.TotalMilliseconds / 100;
        }
        public void SwitchMute()
        {
            mediaElement.IsMuted = !mediaElement.IsMuted;
            if (mediaElement.IsMuted)
                volButt.Data = volButt.Resources["n-0"] as Geometry;
            else
                UpdateVolume();
        }

        private void UpdateScrollLoop()
        {
            while (true)
            {
                Thread.Sleep(1000);
                if (isFullBarVisible)
                    Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Render, (Action)(() =>
                        UpdateTimeLine()
                    ));
            }
        }
        private void UpdateTimeBlock()
        {
            long sec = (long)Math.Round(slider.Value / 10);
            long min = sec / 60;
            sec %= 60;
            timeBlock.Text = $"{string.Format("{0:d2}", min)}:{string.Format("{0:d2}", sec)}";
        }
        private void UpdateEmotion()
        {
            if (fullBar.IsMouseOver)
            {
                int sec = (int)Math.Round(slider.Value / 10);

                try
                {
                    float cur = data[MainWindow.listpos][sec, 1];

                    if (cur > 0.5)
                        currentEmotion.Source = new BitmapImage(new Uri("/Resources/happy.png", UriKind.Relative));
                    else
                        currentEmotion.Source = new BitmapImage(new Uri("/Resources/sad.png", UriKind.Relative));
                }
                catch (IndexOutOfRangeException ex)
                {
                    Console.WriteLine("Error: " + ex.Message);
                }
            }
        }

        private void ResetEmotion()
        {
             currentEmotion.Source = new BitmapImage(new Uri("/Resources/clean.png", UriKind.Relative));
        }
        private void UpdateVolume()
        {
            mediaElement.IsMuted = false;
            mediaElement.Volume = volSlider.Value;

            if (volSlider.Value > 0.66)
                volButt.Data = volButt.Resources["n-3"] as Geometry;
            else
            if (volSlider.Value > 0.33)
                volButt.Data = volButt.Resources["n-2"] as Geometry;
            else
            if (volSlider.Value > 0.01)
                volButt.Data = volButt.Resources["n-1"] as Geometry;
            else
                volButt.Data = volButt.Resources["n-0"] as Geometry;
        }
        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateTimeBlock(); 
            UpdateEmotion();
        }
        private void slider_MouseUp(object sender, MouseButtonEventArgs e)
        {
            mediaElement.Position = new TimeSpan(0, 0, (int)Math.Round(slider.Value / 10));
            isSliderCatched = false;
        }
        private void volSlider_MouseUp(object sender, MouseButtonEventArgs e)
        {
            isVolSliderCatched = false;
        }
        private void slider_MouseMove(object sender, MouseEventArgs args)
        {
            bool Catch = isSliderCatched || isVolSliderCatched;

            if (args.LeftButton == MouseButtonState.Pressed && Catch)
            {
                var thumb = ((sender as Slider).Template.FindName("PART_Track", sender as Slider) as Track).Thumb;
                thumb.RaiseEvent(new MouseButtonEventArgs(args.MouseDevice, args.Timestamp, MouseButton.Left)
                {
                    RoutedEvent = MouseLeftButtonDownEvent,
                    Source = args.Source
                });
            }
        }
        private void volSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateVolume();
        }
        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case (Key.Right):
                    mediaElement.Position = new TimeSpan(0, 0, 0, 0, (int)Math.Floor(slider.Value * 100));
                    isSliderKeyCatch = false;
                    break;

                case (Key.Left):
                    mediaElement.Position = new TimeSpan(0, 0, 0, 0, (int)Math.Floor(slider.Value * 100));
                    isSliderKeyCatch = false;
                    break;
            }
        }
        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            SwitchMute();
        }
        private void Button_MouseEnter_1(object sender, MouseEventArgs e)
        {
            volBar.Visibility = Visibility.Visible;
        }
        private void Button_MouseLeave_1(object sender, MouseEventArgs e)
        {
            if (!volBar.IsMouseOver)
            {
                volBar.Visibility = Visibility.Hidden;
            }
        }
        private void volBar_MouseLeave(object sender, MouseEventArgs e)
        {
            if (!volButt.IsMouseOver)
                volBar.Visibility = Visibility.Hidden;
        }
    }
}
