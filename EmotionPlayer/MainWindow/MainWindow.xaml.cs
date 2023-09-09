using System;
using System.Windows;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;
using System.ComponentModel;
using System.Threading;
using System.Runtime.InteropServices;

namespace EmotionPlayer
{
    public partial class MainWindow : Window
    {
        public static int listpos = -1;
        private bool isPlay = false;
        private bool isFullBarVisible = true;
        private DarkMsgBox msg = null;
        public MainWindow()
        {
            InitializeComponent();

            onSourcesChanged += OnSourcesChanged;
            OnSourcesChanged();

            SetTheme("StandartStyle.xaml");

            scrollUpdater = new Thread(() => UpdateScrollLoop());
            scrollUpdater.Start();

            cursorUpdater = new Thread(() => UpdateCursorLoop());
            cursorUpdater.Start();

            slider.AddHandler(PreviewMouseLeftButtonDownEvent, new RoutedEventHandler((slider, args) =>
            {
                isSliderCatched = true;
            }), true);
            volSlider.AddHandler(PreviewMouseLeftButtonDownEvent, new RoutedEventHandler((volSlider, args) =>
            {
                isVolSliderCatched = true;
            }), true);

            fullBar.Opacity = fullBar.IsMouseOver ? 1 : 0;
            header.Opacity = header.IsMouseOver ? 1 : 0;

            volSlider.Value = mediaElement.Volume;
            PlayNext();
        }

        public void ShowAboutUs()
        {
            bool _isPlay = isPlay;
            mediaElement.Pause();
            new About().ShowDialog();
            if (_isPlay)
                mediaElement.Play();
        }
        public void ShowFileWindow()
        {
            bool isWasEn = isPlay;
            mediaElement.Pause();
            FileWindow FW = new FileWindow(sources,
                "Video|*.ASF;*.WMV;*.WM;*.ASX;*.MP4;*.AVI;*.WMD;*.WVX;*.WPL;*.MPG;*.MPEG;*.M1V;*.MPV2;*.MPA;*.MPE;*.MP2|All files|*.*");
            if (FW.ShowDialog() == true)
            {
                sources.Clear();
                TryAddSources(FW.Sources);
                mediaElement.Source = null;
                mediaElement.Play();
                playButton.Data = playButton.Resources["pausePath"] as Geometry;
                listpos = -1;
                PlayNext();
            }
            else
                if (isWasEn)
                mediaElement.Play();
        }
        public void ShowRateWindow()
        {
            if (FileWindow.data.Any())
            {
                int pos = 0;
                int neg = 0;
                string res;

                double[] classSums = new double[4];
                double[] classMin = new double[4];
                double[] classMax = new double[4];

                // Init values
                for (int j = 0; j < 4; j++)
                {
                    classMin[j] = double.MaxValue;
                    classMax[j] = double.MinValue;
                }

                // Class values
                for (int i = 0; i < FileWindow.data[listpos].GetLength(0); i++)
                {
                    for (int j = 0; j < 4; j++)
                    {
                        // Class sum
                        classSums[j] += FileWindow.data[listpos][i, j];

                        // Min
                        if (FileWindow.data[listpos][i, j] < classMin[j])
                        {
                            classMin[j] = FileWindow.data[listpos][i, j];
                        }

                        // Max
                        if (FileWindow.data[listpos][i, j] > classMax[j])
                        {
                            classMax[j] = FileWindow.data[listpos][i, j];
                        }

                    }
                    if (FileWindow.data[listpos][i, 0] >= 0.5) pos++; else neg++;
                }

                double[] classAverage = new double[4];

                for (int j = 0; j < 4; j++)
                {
                    classAverage[j] = classSums[j] / FileWindow.data[listpos].GetLength(0);
                }
                double value_0 = 0.9;
                double value_1 = 0.1;
                double value_2 = 0.002;
                double value_3 = 0.02;
                if (classAverage[1] > classAverage[0] && classAverage[1] > classAverage[2] && classAverage[1] > classAverage[3])
                {
                    res = "PG-13";
                }
                else if (classAverage[0] < value_0 && classAverage[1] < value_1 && classAverage[2] < value_2 && classAverage[3] < value_3)
                {
                    res = "R";
                }
                else if (classAverage[0] > classAverage[1] && classAverage[0] > classAverage[2] && classAverage[0] > classAverage[3])
                {
                    res = "G";
                }
                else if (classAverage[2] > 0.4 || classAverage[3] > 0.4)
                {
                    if (classAverage[2] > classAverage[3])
                    {
                        res = "PRNY";
                    }
                    else
                    {
                        res = "GORE";
                    }
                }
                else if (classAverage[2] + classAverage[3] > 0.5)
                {
                    res = "BLCK";
                }
                else
                {
                    res = "G";
                }


                msg = new DarkMsgBox(res, pos, neg);
                msg.Show();
            }
        }
        public void PlayPrev()
        {
            if (sources.Count <= 0)
                return;

            listpos = (listpos > 0 ? listpos : sources.Count) - 1;
            mediaElement.Source = new Uri(sources[listpos]);
            Play();
        }
        public void PlayNext()
        {
            if (sources.Count <= 0)
                return;

            listpos = listpos + 1 < sources.Count ? listpos + 1 : 0;
            mediaElement.Source = new Uri(sources[listpos]);
            Play();
        }
        public void Pause()
        {
            mediaElement.Pause();
            playButton.Data = playButton.Resources["playPath"] as Geometry;

            isPlay = false;
        }
        public void Play()
        {
            mediaElement.Play();
            playButton.Data = playButton.Resources["pausePath"] as Geometry;

            isPlay = true;
        }
        public void Stop()
        {
            mediaElement.Stop();
            playButton.Data = playButton.Resources["playPath"] as Geometry;
            timeBlock.Text = "00:00";
            isPlay = false;
        }
        public void SwitchPlay()
        {
            if (isPlay)
                Pause();
            else
                Play();
        }
        public void IncVolume(double a)
        {
            mediaElement.Volume += a;
            volSlider.Value = mediaElement.Volume;
        }
        public void OnWindowMaximized()
        {
            maxButt.Data = maxButt.Resources["toNormal"] as Geometry;
        }
        public void OnWindowNormalized()
        {
            maxButt.Data = maxButt.Resources["toMax"] as Geometry;
        }
        public void SwitchWindowMax()
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void UpdateCursorLoop()
        {
            Point lastSavedPos = new Point();
            Point currPos = new Point();
            int k = 0;

            while (true)
            {
                Thread.Sleep(1000);
                Application.Current.Dispatcher.BeginInvoke((Action)delegate ()
                {
                    currPos = Mouse.GetPosition(this);
                });

                if (Equals(lastSavedPos, currPos))
                {
                    k++;
                    if (k == 5)
                    {
                        Application.Current.Dispatcher.BeginInvoke((Action)delegate ()
                        {
                            Application.Current.MainWindow.Cursor = Cursors.None;
                        });
                        k = 0;
                    }
                }
                else
                {
                    k = 0;
                    lastSavedPos = currPos;
                }

            }
        }
        private void SetTheme(string fileName)
        {
            Uri uri = new Uri(fileName, UriKind.Relative);
            ResourceDictionary resourceDict = Application.LoadComponent(uri) as ResourceDictionary;
            Application.Current.Resources.Clear();
            Application.Current.Resources.MergedDictionaries.Add(resourceDict);
        }
        private void MediaElement_MediaEnded(object sender, RoutedEventArgs e)
        {
            PlayNext();
        }
        private void mediaElement_MediaOpened(object sender, RoutedEventArgs e)
        {
            slider.Maximum = mediaElement.NaturalDuration.TimeSpan.TotalMilliseconds / 100;
            slider.Value = 0;
        }
        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            SwitchWindowMax();
        }
        private void Window_StateChanged(object sender, EventArgs e)
        {
            switch (WindowState)
            {
                case WindowState.Maximized: OnWindowMaximized(); break;
                case WindowState.Normal: OnWindowNormalized(); break;
            }
        }
        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            PlayPrev();
        }
        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            PlayNext();
        }
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case (Key.Right):
                    if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
                        Skip(50);
                    else
                        PlayNext();

                    break;

                case (Key.Left):
                    if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
                        Skip(-50);
                    else
                        PlayPrev();

                    break;

                case (Key.Escape): SwitchWindowMax(); break;

                case (Key.OemPlus): IncVolume(0.1); break;
                case (Key.OemMinus): IncVolume(-0.1); break;

                case (Key.MediaNextTrack): PlayNext(); break;
                case (Key.MediaPreviousTrack): PlayPrev(); break;

                case (Key.MediaPlayPause): SwitchPlay(); break;
                case (Key.MediaStop): SwitchPlay(); break;
                case (Key.Space): SwitchPlay(); break;
                case (Key.P): SwitchPlay(); break;
                case (Key.W): Stop(); break;
                case (Key.S): Stop(); break;

                case (Key.H): ShowAboutUs(); break;
                case (Key.O): ShowFileWindow(); break;
            }
        }
        private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (!fullBar.IsMouseOver)
                if (Mouse.GetPosition(this).Y > 40)
                    SwitchWindowMax();
        }
        private void playButton_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            SwitchPlay();
        }
        private void mediaElement_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            SwitchPlay();
        }
        private void Button_Click_5(object sender, RoutedEventArgs e)
        {
            Close();
        }
        private void headerBar_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }
        private void Button_Click_6(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }
        private void Window_MouseMove(object sender, MouseEventArgs e)
        {
            Cursor = Cursors.Arrow;
        }
        private void fileButt_Click(object sender, RoutedEventArgs e)
        {
            ShowFileWindow();
        }
        private void rateButt_Click(object sender, RoutedEventArgs e)
        {
            ShowRateWindow();
        }
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
                EmptyEl.Focus();
        }
        private void fullBar_MouseEnter(object sender, MouseEventArgs e)
        {
            isFullBarVisible = true;
            UpdateTimeLine();
        }
        private void fullBar_MouseLeave(object sender, MouseEventArgs e)
        {
            isFullBarVisible = false;
            ResetEmotion();
        }
        private void Button_Click_8(object sender, RoutedEventArgs e)
        {
            ShowAboutUs();
        }
        private void MediaElement_Loaded(object sender, RoutedEventArgs e)
        {
            PlayNext();
        }
        private void Window_Closed(object sender, EventArgs e)
        {
            scrollUpdater.Abort();
            cursorUpdater.Abort();
            Application.Current.Shutdown();
        }
        private void PlayButton_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            Stop();
        }
    }
}