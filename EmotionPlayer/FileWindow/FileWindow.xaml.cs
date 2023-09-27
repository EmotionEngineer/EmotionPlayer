using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using OpenCvSharp;

namespace EmotionPlayer
{
    public partial class FileWindow : System.Windows.Window
    {
        private ProgressBarWindow pbw = null;
        public static List<float[,]> data = new List<float[,]>();
        private float cpuUsagePercentage = 0.8f; // Значение загрузки ядер по умолчанию

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
        private async void Button_Click_1(object sender, RoutedEventArgs e)
        {
            data.Clear();
            await ProcessVideosAsync();
        }
        void worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            pbw.UpdateProgress(e.ProgressPercentage);
        }
        private async Task ProcessVideosAsync()
        {
            int numVideo = 0;
            while (numVideo < list.Items.Count)
            {
                pbw = new ProgressBarWindow();
                pbw.Show();
                await ProcessVideoAsync(list.Items[numVideo].ToString());
                pbw.Close();
                numVideo++;
            }
            Close(true);
        }
        private async Task ProcessVideoAsync(string videoFilePath)
        {
            float rev = 0;
            int numFrames = 0;
            string begin = DateTime.Now.ToString("h:mm:ss tt");
            IntPtr progressPtr = Marshal.AllocHGlobal(sizeof(int));

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

            int availableCores = (int)Math.Floor(Environment.ProcessorCount * cpuUsagePercentage); // Получить количество доступных процессорных ядер
            int numThreads = Math.Min(availableCores, numFrames); // Установить число потоков равным минимуму из доступных ядер и числа кадров
            int framesPerThread = numFrames / numThreads;

            // Асинхронно вызываем videoSentiment
            var inferenceTasks = Enumerable.Range(0, numThreads).Select(threadIndex => Task.Run(() =>
            {
                int startIndex = threadIndex * framesPerThread;
                int endIndex = (threadIndex == numThreads - 1) ? numFrames : (threadIndex + 1) * framesPerThread;

                float[,,,] framesChunk = new float[endIndex - startIndex, 3, targetHeight, targetWidth];
                float[,] predictionsChunk = new float[endIndex - startIndex, 4];

                for (int i = startIndex; i < endIndex; i++)
                {
                    Buffer.BlockCopy(framesArray, i * 3 * targetHeight * targetWidth * sizeof(float),
                                     framesChunk, (i - startIndex) * 3 * targetHeight * targetWidth * sizeof(float),
                                     3 * targetHeight * targetWidth * sizeof(float));
                }

                EmotionLib_videoSentiment(framesChunk, endIndex - startIndex, predictionsChunk, progressPtr);

                // Копируем результаты из подмассива в общий массив
                for (int i = startIndex; i < endIndex; i++)
                {
                    Buffer.BlockCopy(predictionsChunk, (i - startIndex) * 4 * sizeof(float),
                                     tensorPredictions, i * 4 * sizeof(float),
                                     4 * sizeof(float));
                }
            })).ToArray();

            while (true)
            {
                await Task.Delay(1000);
                int currentProgress = Marshal.ReadInt32(progressPtr);
                rev = (float)currentProgress / numFrames;
                pbw.UpdateProgress((int)Math.Round(rev * 100));

                if (currentProgress >= numFrames)
                {
                    break; // Выходим из цикла, когда достигнут конец
                }
            }

            await Task.WhenAll(inferenceTasks);
            data.Add(tensorPredictions);
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