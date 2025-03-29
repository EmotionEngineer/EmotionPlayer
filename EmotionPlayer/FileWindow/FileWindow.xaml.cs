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
        private float cpuUsagePercentage = 0.8f; // Define CPU usage

        [DllImport("positiveness", CallingConvention = CallingConvention.Cdecl)]
        public static extern void positiveness_VideoInference([In, Out] float[,,,] frames, int num_frames, [In, Out] float[,] tensor_predictions, IntPtr progress);
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

            // Get FPS
            double fps = videoCapture.Get(VideoCaptureProperties.Fps);

            // Count frames
            numFrames = (int)(videoCapture.Get(VideoCaptureProperties.FrameCount) / fps);

            // Array to keep frames
            int targetWidth = 227;
            int targetHeight = 227;
            float[,,,] framesArray = new float[numFrames, 3, targetHeight, targetWidth];
            float[] meanValues = { 104.00698793f, 116.66876762f, 122.67891434f };

            for (int i = 0; i < numFrames; i++)
            {
                // Extract frames with fps value
                int frameIndex = (int)Math.Floor(i * fps);
                videoCapture.Set(VideoCaptureProperties.PosFrames, frameIndex);
                Mat frame = new Mat();
                videoCapture.Read(frame);

                if (!frame.Empty())
                {
                    // Resize 256x256
                    Cv2.Resize(frame, frame, new OpenCvSharp.Size(256, 256));

                    // Crop
                    int cropX = (frame.Width - 227) / 2;
                    int cropY = (frame.Height - 227) / 2;
                    OpenCvSharp.Rect cropRect = new OpenCvSharp.Rect(cropX, cropY, 227, 227);
                    Mat croppedFrame = new Mat(frame, cropRect);

                    // Transform
                    for (int y = 0; y < targetHeight; y++)
                    {
                        for (int x = 0; x < targetWidth; x++)
                        {
                            Vec3b pixelColor = croppedFrame.At<Vec3b>(y, x);
                            framesArray[i, 0, y, x] = pixelColor[0] - meanValues[0]; // Blue
                            framesArray[i, 1, y, x] = pixelColor[1] - meanValues[1]; // Green
                            framesArray[i, 2, y, x] = pixelColor[2] - meanValues[2]; // Red
                        }
                    }
                }
            }


            // Close video
            videoCapture.Release();

            // Array with results
            float[,] tensorPredictions = new float[numFrames, 2];

            // Progress
            int progressValue = 0; // Init progress
            Marshal.WriteInt32(progressPtr, progressValue);

            int availableCores = Math.Max((int)Math.Floor(Environment.ProcessorCount * cpuUsagePercentage), 1); // Get the number of available CPU cores (at least 1)
            int numThreads = Math.Min(availableCores, numFrames); // Choose core number
            int framesPerThread = numFrames / numThreads;

            bool[] threadCompletion = new bool[numThreads];

            var inferenceTasks = Enumerable.Range(0, numThreads).Select(threadIndex => Task.Run(() =>
            {
                int startIndex = threadIndex * framesPerThread;
                int endIndex = (threadIndex == numThreads - 1) ? numFrames : (threadIndex + 1) * framesPerThread;

                float[,,,] framesChunk = new float[endIndex - startIndex, 3, targetHeight, targetWidth];
                float[,] predictionsChunk = new float[endIndex - startIndex, 2];

                for (int i = startIndex; i < endIndex; i++)
                {
                    Buffer.BlockCopy(framesArray, i * 3 * targetHeight * targetWidth * sizeof(float),
                                     framesChunk, (i - startIndex) * 3 * targetHeight * targetWidth * sizeof(float),
                                     3 * targetHeight * targetWidth * sizeof(float));
                }

                positiveness_VideoInference(framesChunk, endIndex - startIndex, predictionsChunk, progressPtr);

                // Copy results
                for (int i = startIndex; i < endIndex; i++)
                {
                    Buffer.BlockCopy(predictionsChunk, (i - startIndex) * 2 * sizeof(float),
                                     tensorPredictions, i * 2 * sizeof(float),
                                     2 * sizeof(float));
                }

                threadCompletion[threadIndex] = true;
            })).ToArray();

            while (!threadCompletion.All(x => x))
            {
                await Task.Delay(1000);
                int currentProgress = Marshal.ReadInt32(progressPtr);
                rev = (float)currentProgress / numFrames;
                pbw.UpdateProgress((int)Math.Round(rev * 100));
            }


            await Task.WhenAll(inferenceTasks);
            data.Add(tensorPredictions);
            Console.WriteLine(tensorPredictions[0, 0]);
            Console.WriteLine(tensorPredictions[0, 1]);
            Console.WriteLine(tensorPredictions[10, 0]);
            Console.WriteLine(tensorPredictions[10, 1]);
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