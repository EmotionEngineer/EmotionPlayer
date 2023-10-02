using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using OpenCvSharp;

namespace EmotionPlayer
{
    public partial class FileWindow : System.Windows.Window
    {
        private BackgroundWorker worker = new BackgroundWorker();
        private ProgressBarWindow pbw = null;
        public static List<float[,]> data = new List<float[,]>();
        private int gc = 0;
        int numFrames = 0; // Number of frames to extract
        IntPtr progressPtr = Marshal.AllocHGlobal(sizeof(int));
        [DllImport("emotionLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void EmotionLib_videoSentiment([In, Out] float[,,,] frames, int num_frames, [In, Out] float[,] tensor_predictions, IntPtr progress);
        private string filter;

        public FileWindow(IEnumerable<string> sources, string filter = "Все файлы|*.*")
        {
            InitializeComponent();

            this.filter = filter;
            list.Items.AddRange(sources);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Close(false);
        }
        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            data.Clear();
            for (int z = 0; z < list.Items.Count; z++) ShowProgress();
        }
        void worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            pbw.UpdateProgress(e.ProgressPercentage);
        }
        void ShowProgress()
        {
            try
            {
                worker.WorkerReportsProgress = true;
                worker.DoWork += ProcessLogsAsynch;
                worker.ProgressChanged += worker_ProgressChanged;
                while (!worker.IsBusy)
                {
                    worker.RunWorkerAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "ERROR");
            }
        }
        private void ProcessLogsAsynch(object sender, DoWorkEventArgs e)
        {
            float rev = 0;
            string begin = DateTime.Now.ToString("h:mm:ss tt");
            Dispatcher.Invoke(() =>
            {
                new Thread(() =>
                {
                    Thread.CurrentThread.IsBackground = true;
                    string videoFilePath = list.Items[gc].ToString(); // Video path
                    VideoCapture videoCapture = new VideoCapture(videoFilePath);

                    // OpenCV check
                    if (!videoCapture.IsOpened())
                    {
                        Console.WriteLine("Open video error.");
                        return;
                    }

                    // Получите частоту кадров видео
                    double fps = videoCapture.Get(VideoCaptureProperties.Fps);

                    // Вычислите количество кадров для извлечения
                    numFrames = (int)(videoCapture.Get(VideoCaptureProperties.FrameCount) / fps);

                    // Создайте массив для хранения кадров
                    int targetWidth = 224;
                    int targetHeight = 224;
                    float[,,,] framesArray = new float[numFrames, 3, targetHeight, targetWidth];

                    for (int i = 0; i < numFrames; i++)
                    {
                        // Извлекаем кадр из видео с учетом fps
                        int frameIndex = (int)Math.Floor(i * fps);
                        videoCapture.Set(VideoCaptureProperties.PosFrames, frameIndex);
                        Mat frame = new Mat();
                        videoCapture.Read(frame);

                        if (!frame.Empty())
                        {
                            // Ресайзинг кадра до требуемых размеров
                            Cv2.Resize(frame, frame, new OpenCvSharp.Size(targetWidth, targetHeight));

                            // Преобразуем изображение в нужный формат
                            for (int y = 0; y < targetHeight; y++)
                            {
                                for (int x = 0; x < targetWidth; x++)
                                {
                                    Vec3b pixelColor = frame.At<Vec3b>(y, x);
                                    framesArray[i, 0, y, x] = pixelColor[2] / 255.0f;
                                    framesArray[i, 1, y, x] = pixelColor[1] / 255.0f;
                                    framesArray[i, 2, y, x] = pixelColor[0] / 255.0f;
                                }
                            }
                        }
                    }

                    // Закрываем видеофайл
                    videoCapture.Release();

                    // Создаем массив для хранения результатов
                    float[,] tensorPredictions = new float[numFrames, 4];

                    // Прогресс
                    int progressValue = 0; // Исходное значение progress
                    Marshal.WriteInt32(progressPtr, progressValue);
                    EmotionLib_videoSentiment(framesArray, numFrames, tensorPredictions, progressPtr);
                    data.Add(tensorPredictions);
                }).Start();

                pbw = new ProgressBarWindow();
                pbw.Show();
            });

            while (rev != 1)
            {
                Thread.Sleep(100);
                rev = (float)Marshal.ReadInt32(progressPtr) / (float)numFrames;
                (sender as BackgroundWorker).ReportProgress((int)Math.Round(rev * 100));
            }

            Dispatcher.Invoke(() =>
            {
                if (gc == list.Items.Count - 1) Close(true);
                gc++;
            });
        }
        private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }
        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            OpenSourcesDialog();
        }
        private void list_MouseDown(object sender, MouseButtonEventArgs e)
        {
            list.RemoveSelection();
        }
        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            list.Items.TryRemoveAt(list.SelectedIndex);
        }
        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            list.Items.Clear();
        }
        private void Button_Click_5(object sender, RoutedEventArgs e)
        {
            list.TryMoveSelected(list.SelectedIndex + 1);
        }
        private void Button_Click_6(object sender, RoutedEventArgs e)
        {
            list.TryMoveSelected(list.SelectedIndex - 1);
        }
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case (Key.S): Close(true); break;
                case (Key.Escape): Close(false); break;
                case (Key.A):
                case (Key.OemPlus): OpenSourcesDialog(); break;
                case (Key.D):
                case (Key.OemMinus): list.Items.TryRemoveAt(list.SelectedIndex); break;
                case (Key.F): list.RemoveSelection(); break;
                case (Key.C): list.Items.Clear(); break;
                case (Key.N): list.TryMoveSelected(list.SelectedIndex + 1); break;
                case (Key.U): list.TryMoveSelected(list.SelectedIndex - 1); break;
            }
        }
    }
}