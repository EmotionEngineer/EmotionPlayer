using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace EmotionPlayer
{
    public partial class MainWindow
    {
        private Thread scrollUpdater, cursorUpdater;
        private bool isSliderCatched;
        private bool isVolSliderCatched;
        private bool isSliderKeyCatch;

        /// <summary>
        /// Skips playback forward/backward by delta units of the timeline slider.
        /// </summary>
        public void Skip(int delta)
        {
            if (!isSliderKeyCatch)
            {
                UpdateTimeLine();
                isSliderKeyCatch = true;
            }

            slider.Value += delta;
        }

        /// <summary>
        /// Synchronizes the slider with the current MediaElement position.
        /// </summary>
        public void UpdateTimeLine()
        {
            if (!isSliderCatched && !isSliderKeyCatch && mediaElement.NaturalDuration.HasTimeSpan)
            {
                slider.Value = mediaElement.Position.TotalMilliseconds / 100.0;
            }
        }

        /// <summary>
        /// Toggles mute state and updates volume icon.
        /// </summary>
        public void SwitchMute()
        {
            mediaElement.IsMuted = !mediaElement.IsMuted;

            if (mediaElement.IsMuted)
                volButt.Data = volButt.Resources["n-0"] as Geometry;
            else
                UpdateVolume();
        }

        /// <summary>
        /// Periodically updates the timeline slider while the full control bar is visible.
        /// Runs on a background thread and marshals updates to the UI dispatcher.
        /// </summary>
        private void UpdateScrollLoop()
        {
            while (true)
            {
                Thread.Sleep(1000);

                if (isFullBarVisible)
                {
                    Application.Current.Dispatcher.BeginInvoke(
                        DispatcherPriority.Render,
                        (Action)UpdateTimeLine);
                }
            }
        }

        /// <summary>
        /// Updates the textual time indicator (mm:ss) based on slider value.
        /// </summary>
        private void UpdateTimeBlock()
        {
            long sec = (long)Math.Round(slider.Value / 10.0);
            long min = sec / 60;
            sec %= 60;

            timeBlock.Text = $"{min:00}:{sec:00}";
        }

        /// <summary>
        /// Updates the emoji that reflects current frame positiveness.
        /// Takes into account sampling interval used by the positiveness model.
        /// </summary>
        private void UpdateEmotion()
        {
            if (!fullBar.IsMouseOver)
                return;

            if (listpos < 0 || listpos >= data.Count)
                return;

            var result = data[listpos];
            if (result?.tensorPredictions == null || result.tensorPredictions.Length == 0)
                return;

            int interval = result.frameSecInterval > 0 ? result.frameSecInterval : 1;

            // Current playback time in seconds (approximation from slider).
            int sec = (int)Math.Round(slider.Value / 10.0);

            // Convert playback seconds to index in the prediction tensor.
            int index = sec / interval;
            int maxIndex = result.tensorPredictions.GetLength(0) - 1;

            if (index < 0)
                return;

            if (index > maxIndex)
                index = maxIndex;

            float cur = result.tensorPredictions[index, 1];

            string uri =
                cur > 0.5f
                    ? "/Resources/happy.png"
                    : "/Resources/sad.png";

            currentEmotion.Source = new BitmapImage(new Uri(uri, UriKind.Relative));
        }

        /// <summary>
        /// Resets emoji to a neutral state.
        /// </summary>
        private void ResetEmotion()
        {
            currentEmotion.Source = new BitmapImage(new Uri("/Resources/clean.png", UriKind.Relative));
        }

        /// <summary>
        /// Applies current slider value to the MediaElement volume
        /// and updates the volume icon.
        /// </summary>
        private void UpdateVolume()
        {
            mediaElement.IsMuted = false;
            mediaElement.Volume = volSlider.Value;

            double v = volSlider.Value;
            if (v > 0.66)
                volButt.Data = volButt.Resources["n-3"] as Geometry;
            else if (v > 0.33)
                volButt.Data = volButt.Resources["n-2"] as Geometry;
            else if (v > 0.01)
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
            mediaElement.Position = new TimeSpan(0, 0, (int)Math.Round(slider.Value / 10.0));
            isSliderCatched = false;
        }

        private void volSlider_MouseUp(object sender, MouseButtonEventArgs e)
        {
            isVolSliderCatched = false;
        }

        private void slider_MouseMove(object sender, MouseEventArgs args)
        {
            bool isDragging = isSliderCatched || isVolSliderCatched;

            if (args.LeftButton == MouseButtonState.Pressed && isDragging)
            {
                if (sender is Slider s &&
                    s.Template.FindName("PART_Track", s) is Track track &&
                    track.Thumb != null)
                {
                    track.Thumb.RaiseEvent(new MouseButtonEventArgs(
                        args.MouseDevice,
                        args.Timestamp,
                        MouseButton.Left)
                    {
                        RoutedEvent = MouseLeftButtonDownEvent,
                        Source = args.Source
                    });
                }
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
                case Key.Right:
                case Key.Left:
                    mediaElement.Position = new TimeSpan(
                        0, 0, 0, 0,
                        (int)Math.Floor(slider.Value * 100.0));
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