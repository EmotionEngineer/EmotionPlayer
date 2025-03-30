using System;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Linq;

namespace EmotionPlayer
{
    internal static class Inferencer
    {
        private static float cpuUsagePercentage = 0.8f; // CPU usage (%)

        [DllImport("filter.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void filter_VideoInference([In, Out] float[,,,] frames, int num_frames, [In, Out] float[,] results, IntPtr progress);

        [DllImport("positiveness.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void positiveness_VideoInference([In, Out] float[,,,] frames, int num_frames, [In, Out] float[,] results, IntPtr progress);

        [DllImport("samp.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern double predictSAMP(string epp_path, string efp_path);


        private static bool IsFrameMostlyDark(float[,,,] frame, int frameInd = 0, float threshold = 0.02f)
        {
            int width = frame.GetLength(3);
            int height = frame.GetLength(2);
            float sum = 0;
            int count = 0;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // Calculate the average value of pixels across all channels
                    sum += (frame[frameInd, 0, y, x] + frame[frameInd, 1, y, x] + frame[frameInd, 2, y, x]) / 3;
                    count++;
                }
            }

            // Calculate the average brightness of the frame
            float averageBrightness = sum / count;

            // If the average value is below the threshold, consider the frame dark
            return averageBrightness < threshold;
        }
        private static int GetFrameSecInterval(double totalSeconds)
        {
            const int secPerMinute = 60;
            const int secPerHour = secPerMinute * 60;

            if (totalSeconds <= 20 * secPerMinute)
            {
                return 1; // Up to 20 minutes - every second
            }
            else if (totalSeconds <= 1 * secPerHour)
            {
                return 2; // From 20 minutes to 1 hour - every 2 seconds
            }
            else if (totalSeconds <= 1.8 * secPerHour)
            {
                return 3; // From 1 hour to 1.8 hours - every 3 seconds
            }
            else if (totalSeconds <= 2.1 * secPerHour)
            {
                return 4; // From 1.8 hours to 2.1 hours - every 4 seconds
            }
            else if (totalSeconds <= 3 * secPerHour)
            {
                return 5; // From 2.1 hours to 3 hours - every 5 seconds
            }
            else
            {
                return 6; // More than 3 hours - every 6 seconds
            }
        }
        private static float[,,,] LoadVideoFrames(string videoFilePath, out int numFrames, out int frameSecInterval, int targetWidth = 256, int targetHeight = 256, bool isRGB = true, bool useImageNetMean = false)
        {
            VideoCapture videoCapture = new VideoCapture(videoFilePath);
            frameSecInterval = 3;

            if (!videoCapture.IsOpened())
            {
                Console.WriteLine("Error opening video.");
                numFrames = 0;
                return null;
            }

            Console.WriteLine($"Processing video file: {Path.GetFileName(videoFilePath)}");

            double totalSeconds = videoCapture.Get(VideoCaptureProperties.FrameCount) / videoCapture.Get(VideoCaptureProperties.Fps);
            frameSecInterval = GetFrameSecInterval(totalSeconds);
            Console.WriteLine($"Video duration: {TimeSpan.FromSeconds(totalSeconds)}");
            Console.WriteLine($"Selected frame interval: {frameSecInterval} second(s)");

            int frameInterval = (int)Math.Round(videoCapture.Get(VideoCaptureProperties.Fps) * frameSecInterval);
            numFrames = (int)(videoCapture.Get(VideoCaptureProperties.FrameCount) / frameInterval);

            float[,,,] framesArray = new float[numFrames, 3, targetHeight, targetWidth];
            float[] meanValues = { 104.00698793f, 116.66876762f, 122.67891434f };

            for (int i = 0; i < numFrames; i++)
            {
                int frameIndex = i * frameInterval;
                videoCapture.Set(VideoCaptureProperties.PosFrames, frameIndex);
                using (Mat frame = new Mat())
                {
                    videoCapture.Read(frame);
                    if (!frame.Empty())
                    {
                        Cv2.Resize(frame, frame, new OpenCvSharp.Size(targetWidth, targetHeight));

                        for (int y = 0; y < targetHeight; y++)
                        {
                            for (int x = 0; x < targetWidth; x++)
                            {
                                Vec3b pixelColor = frame.At<Vec3b>(y, x);
                                float b = pixelColor[0];
                                float g = pixelColor[1];
                                float r = pixelColor[2];

                                if (useImageNetMean)
                                {
                                    // ImageNet Mean Norm
                                    if (isRGB)
                                    {
                                        framesArray[i, 0, y, x] = r - meanValues[2];
                                        framesArray[i, 1, y, x] = g - meanValues[1];
                                        framesArray[i, 2, y, x] = b - meanValues[0];
                                    }
                                    else
                                    {
                                        framesArray[i, 0, y, x] = b - meanValues[0];
                                        framesArray[i, 1, y, x] = g - meanValues[1];
                                        framesArray[i, 2, y, x] = r - meanValues[2];
                                    }
                                }
                                else
                                {
                                    // MinMax Norm 0..1
                                    if (isRGB)
                                    {
                                        framesArray[i, 0, y, x] = r / 255.0f;
                                        framesArray[i, 1, y, x] = g / 255.0f;
                                        framesArray[i, 2, y, x] = b / 255.0f;
                                    }
                                    else
                                    {
                                        framesArray[i, 0, y, x] = b / 255.0f;
                                        framesArray[i, 1, y, x] = g / 255.0f;
                                        framesArray[i, 2, y, x] = r / 255.0f;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            videoCapture.Release();
            return framesArray;
        }
        private static async Task ProcessVideoPositivenessAsync(string videoName,
int frameSecInterval, float[,,,] framesArray, int numFrames, InferenceContext ctx, int targetWidth = 227, int targetHeight = 227)
        {
            Console.WriteLine("Processing Positiveness model...");
            float rev = 0;
            string begin = DateTime.Now.ToString("h:mm:ss tt");
            IntPtr progressPtr = Marshal.AllocHGlobal(sizeof(int));

            // Create an array to store the results
            float[,] tensorPredictions = new float[numFrames, 2];

            // Progress
            int progressValue = 0; // Initial progress value
            Marshal.WriteInt32(progressPtr, progressValue);

            int availableCores = Math.Max((int)Math.Floor(Environment.ProcessorCount * cpuUsagePercentage), 1); // Get the number of available CPU cores (at least 1)
            int numThreads = Math.Min(availableCores, numFrames); // Set the number of threads to the minimum of available cores and frame count
            int framesPerThread = numFrames / numThreads;

            bool[] threadCompletion = new bool[numThreads];

            // Asynchronously call the filter function
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

                // Copy the results from the subarray into the main array
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
                await Task.Delay(ctx.millisecondsDelay);
                int currentProgress = Marshal.ReadInt32(progressPtr);
                rev = (float)currentProgress / numFrames;
                ctx.updateProgress?.Invoke((int)Math.Round(rev * 100));
            }

            await Task.WhenAll(inferenceTasks);

            ctx?.setPositivenessTensorPredictions?.Invoke(tensorPredictions);

            // Serialize the results
            string directoryPath = "Output"; // Folder name
            string filePath = Path.Combine(directoryPath, $"{videoName}.epp"); // Full file path

            // Check if the folder exists and create it if not
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                using (var writer = new BinaryWriter(fileStream))
                {
                    writer.Write(numFrames); // Write the number of frames at the start of the file
                    writer.Write(frameSecInterval); // Write the interval between frames (in seconds)
                    for (int i = 0; i < numFrames; i++)
                    {
                        for (int j = 0; j < 2; j++)
                        {
                            writer.Write(tensorPredictions[i, j]); // Write the result for each frame
                        }
                    }
                }
            }

            Marshal.FreeHGlobal(progressPtr); // Free memory allocated for progress

            Console.WriteLine("Positiveness model complete.");
            string end = DateTime.Now.ToString("h:mm:ss tt");
            Console.WriteLine($"Task started at {begin} and ended at {end}");
        }
        private static async Task ProcessVideoFilterAsync(string videoName,
int frameSecInterval, float[,,,] framesArray, int numFrames, InferenceContext ctx, int targetWidth = 224, int targetHeight = 224)
        {
            Console.WriteLine("Processing Filter model...");
            float rev = 0;
            string begin = DateTime.Now.ToString("h:mm:ss tt");
            IntPtr progressPtr = Marshal.AllocHGlobal(sizeof(int));

            // Create an array to store the results
            float[,] tensorPredictions = new float[numFrames, 3];

            // Progress
            int progressValue = 0; // Initial progress value
            Marshal.WriteInt32(progressPtr, progressValue);

            int availableCores = Math.Max((int)Math.Floor(Environment.ProcessorCount * cpuUsagePercentage), 1); // Get the number of available CPU cores (at least 1)
            int numThreads = Math.Min(availableCores, numFrames); // Set the number of threads to the minimum of available cores and frame count
            int framesPerThread = numFrames / numThreads;

            bool[] threadCompletion = new bool[numThreads];

            // Asynchronously call the filter function
            var inferenceTasks = Enumerable.Range(0, numThreads).Select(threadIndex => Task.Run(() =>
            {
                int startIndex = threadIndex * framesPerThread;
                int endIndex = (threadIndex == numThreads - 1) ? numFrames : (threadIndex + 1) * framesPerThread;

                float[,,,] framesChunk = new float[endIndex - startIndex, 3, targetHeight, targetWidth];
                float[,] predictionsChunk = new float[endIndex - startIndex, 3];

                for (int i = startIndex; i < endIndex; i++)
                {
                    Buffer.BlockCopy(framesArray, i * 3 * targetHeight * targetWidth * sizeof(float),
                                     framesChunk, (i - startIndex) * 3 * targetHeight * targetWidth * sizeof(float),
                                     3 * targetHeight * targetWidth * sizeof(float));
                }

                filter_VideoInference(framesChunk, endIndex - startIndex, predictionsChunk, progressPtr);

                // Copy the results from the subarray into the main array
                for (int i = startIndex; i < endIndex; i++)
                {
                    Buffer.BlockCopy(predictionsChunk, (i - startIndex) * 3 * sizeof(float),
                                     tensorPredictions, i * 3 * sizeof(float),
                                     3 * sizeof(float));
                }

                threadCompletion[threadIndex] = true;
            })).ToArray();

            while (!threadCompletion.All(x => x))
            {
                await Task.Delay(ctx.millisecondsDelay);
                int currentProgress = Marshal.ReadInt32(progressPtr);
                rev = (float)currentProgress / numFrames;
                ctx.updateProgress?.Invoke((int)Math.Round(rev * 100));
            }

            await Task.WhenAll(inferenceTasks);

            // Calculate statistics and print predictions for each frame
            for (int i = 0; i < numFrames; i++)
            {
                // Check if the frame is mostly dark
                if (IsFrameMostlyDark(framesArray, i))
                {
                    tensorPredictions[i, 0] = -1; // Set -1 for dark frames
                    tensorPredictions[i, 1] = -1;
                    tensorPredictions[i, 2] = -1;
                }
            }

            // Serialize the results
            string directoryPath = "Output"; // Folder name
            string filePath = Path.Combine(directoryPath, $"{videoName}.efp"); // Full file path

            // Check if the folder exists and create it if not
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                using (var writer = new BinaryWriter(fileStream))
                {
                    writer.Write(numFrames); // Write the number of frames at the start of the file
                    writer.Write(frameSecInterval); // Write the interval between frames (in seconds)
                    for (int i = 0; i < numFrames; i++)
                    {
                        for (int j = 0; j < 3; j++)
                        {
                            writer.Write(tensorPredictions[i, j]); // Write the result for each frame
                        }
                    }
                }
            }

            Marshal.FreeHGlobal(progressPtr); // Free memory allocated for progress

            Console.WriteLine("Filter model complete.");
            string end = DateTime.Now.ToString("h:mm:ss tt");
            Console.WriteLine($"Task started at {begin} and ended at {end}");
        }
        private static string GetMpaaRating(double rating)
        {
            if (rating < 0.0796)
            {
                return "G";
            }
            else if (rating < 0.216)
            {
                return "PG";
            }
            else if (rating < 0.464)
            {
                return "PG-13";
            }
            else
            {
                return "R";
            }
        }
        public static void ClassifyVideo(string videoName, InferenceContext ctx)
        {
            // Define paths to the EPP and EFP files
            string eppPath = Path.Combine("Output", $"{videoName}.epp");
            string efpPath = Path.Combine("Output", $"{videoName}.efp");

            // Check if both EPP and EFP files exist
            if (!File.Exists(eppPath) || !File.Exists(efpPath))
            {
                Console.WriteLine($"EPP or EFP file missing for {videoName}");
                return;
            }

            try
            {
                // Call the DLL function to predict the Safeness and MPAA value
                double result = predictSAMP(eppPath, efpPath);

                // Check for NaN result
                if (double.IsNaN(result))
                {
                    Console.WriteLine($"Error: The prediction for video '{videoName}' returned an invalid result (NaN).");
                    return;
                }

                // Interpret the result
                if (result == 2.0)
                {
                    // Video is classified as unsafe and should be blocked.
                    ctx?.setInterpretedResult?.Invoke("Unsafe");
                }
                else
                {
                    // Video is classified with MPAA rating: `GetMpaaRating(result)`.
                    ctx?.setInterpretedResult?.Invoke(GetMpaaRating(result));
                }
            }
            catch (Exception ex)
            {
                // Handle any exceptions that might occur
                Console.WriteLine($"An error occurred while classifying the video '{videoName}': {ex.Message}");
            }
        }
        public static async Task Main(string videoFilePath, InferenceContext ctx)
        {
            int numFrames;
            int frameSecInterval;
            float[,,,] framesArrayPositiveness = LoadVideoFrames(videoFilePath, out numFrames, out frameSecInterval, 227, 227, isRGB: false, useImageNetMean: true);
            if (framesArrayPositiveness != null)
            {
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(videoFilePath);
                await ProcessVideoPositivenessAsync(fileNameWithoutExtension, frameSecInterval, framesArrayPositiveness, numFrames, ctx);
            }

            framesArrayPositiveness = null;

            float[,,,] framesArrayFilter = LoadVideoFrames(videoFilePath, out numFrames, out frameSecInterval, 224, 224);
            if (framesArrayFilter != null)
            {
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(videoFilePath);
                await ProcessVideoFilterAsync(fileNameWithoutExtension, frameSecInterval, framesArrayFilter, numFrames, ctx);
                ClassifyVideo(fileNameWithoutExtension, ctx);
            }

            framesArrayFilter = null;

            Console.WriteLine("File processing completed.");
            Console.WriteLine("");
        }
    }
}
