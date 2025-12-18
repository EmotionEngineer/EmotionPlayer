using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using OpenCvSharp;

namespace EmotionPlayer
{
    internal static class Inferencer
    {
        private const float CpuUsagePercentage = 0.8f;

        #region Native functions

        [DllImport("filter.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void filter_VideoInference(
            [In, Out] float[,,,] frames,
            int num_frames,
            [In, Out] float[,] results,
            IntPtr progress);

        [DllImport("positiveness.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern void positiveness_VideoInference(
            [In, Out] float[,,,] frames,
            int num_frames,
            [In, Out] float[,] results,
            IntPtr progress);

        [DllImport("samp.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern double predictSAMP(string epp_path, string efp_path);

        #endregion

        #region Helpers

        private static bool IsFrameMostlyDark(float[,,,] frames, int frameIndex = 0, float threshold = 0.02f)
        {
            int width = frames.GetLength(3);
            int height = frames.GetLength(2);
            float sum = 0;
            int count = 0;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    sum += (frames[frameIndex, 0, y, x] +
                            frames[frameIndex, 1, y, x] +
                            frames[frameIndex, 2, y, x]) / 3.0f;
                    count++;
                }
            }

            float averageBrightness = sum / Math.Max(count, 1);
            return averageBrightness < threshold;
        }

        private static int GetFrameSecInterval(double totalSeconds)
        {
            const int secPerMinute = 60;
            const int secPerHour = secPerMinute * 60;

            if (totalSeconds <= 20 * secPerMinute)
                return 1;
            if (totalSeconds <= 1 * secPerHour)
                return 2;
            if (totalSeconds <= 1.8 * secPerHour)
                return 3;
            if (totalSeconds <= 2.1 * secPerHour)
                return 4;
            if (totalSeconds <= 3 * secPerHour)
                return 5;

            return 6;
        }

        private static float[,,,] LoadVideoFrames(
            string videoFilePath,
            out int numFrames,
            out int frameSecInterval,
            int targetWidth = 256,
            int targetHeight = 256,
            bool isRgbOrder = true,
            bool useImageNetMean = false)
        {
            frameSecInterval = 3;
            numFrames = 0;

            using (var videoCapture = new VideoCapture(videoFilePath))
            {
                if (!videoCapture.IsOpened())
                {
                    Console.WriteLine($"Failed to open video: {videoFilePath}");
                    return null;
                }

                double frameCount = videoCapture.Get(VideoCaptureProperties.FrameCount);
                if (frameCount <= 0)
                {
                    Console.WriteLine($"Video has no frames: {videoFilePath}");
                    return null;
                }

                double fps = videoCapture.Get(VideoCaptureProperties.Fps);
                if (fps <= 0 || double.IsNaN(fps) || double.IsInfinity(fps))
                {
                    fps = 30;
                }

                double totalSeconds = frameCount / fps;
                frameSecInterval = GetFrameSecInterval(totalSeconds);

                Console.WriteLine($"Processing video file: {Path.GetFileName(videoFilePath)}");
                Console.WriteLine($"Duration: {TimeSpan.FromSeconds(totalSeconds)}");
                Console.WriteLine($"Sampling interval: {frameSecInterval} second(s)");

                int frameInterval = (int)Math.Round(fps * frameSecInterval);
                if (frameInterval <= 0)
                    frameInterval = 1;

                numFrames = (int)Math.Ceiling(totalSeconds / frameSecInterval);
                if (numFrames <= 0)
                    numFrames = 1;

                var framesArray = new float[numFrames, 3, targetHeight, targetWidth];

                float[] meanValues = { 104.00698793f, 116.66876762f, 122.67891434f };

                for (int i = 0; i < numFrames; i++)
                {
                    long rawIndex = (long)i * frameInterval;
                    int frameIndex = (int)Math.Min(rawIndex, (long)frameCount - 1);

                    videoCapture.Set(VideoCaptureProperties.PosFrames, frameIndex);

                    using (var frame = new Mat())
                    {
                        if (!videoCapture.Read(frame) || frame.Empty())
                            continue;

                        Cv2.Resize(frame, frame, new Size(targetWidth, targetHeight));

                        for (int y = 0; y < targetHeight; y++)
                        {
                            for (int x = 0; x < targetWidth; x++)
                            {
                                Vec3b pixel = frame.At<Vec3b>(y, x);
                                float b = pixel[0];
                                float g = pixel[1];
                                float r = pixel[2];

                                if (useImageNetMean)
                                {
                                    if (isRgbOrder)
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
                                    if (isRgbOrder)
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

                return framesArray;
            }
        }

        #endregion

        #region Model runners

        private static async Task ProcessVideoPositivenessAsync(
            string videoName,
            int frameSecInterval,
            float[,,,] framesArray,
            int numFrames,
            InferenceContext ctx)
        {
            const string stageName = "Positiveness";

            if (framesArray == null || numFrames <= 0)
            {
                ctx?.setPositivenessTensorPredictions?.Invoke(new float[0, 2], frameSecInterval);
                ctx?.updateProgress?.Invoke(100, stageName, videoName);
                return;
            }

            Console.WriteLine("Running positiveness model...");
            string begin = DateTime.Now.ToString("HH:mm:ss");

            int targetHeight = framesArray.GetLength(2);
            int targetWidth = framesArray.GetLength(3);
            float[,] tensorPredictions = new float[numFrames, 2];

            IntPtr progressPtr = Marshal.AllocHGlobal(sizeof(int));
            try
            {
                Marshal.WriteInt32(progressPtr, 0);

                int availableCores = Math.Max((int)Math.Floor(Environment.ProcessorCount * CpuUsagePercentage), 1);
                int numThreads = Math.Min(availableCores, numFrames);
                if (numThreads <= 0)
                    numThreads = 1;

                var inferenceTasks = Enumerable.Range(0, numThreads)
                    .Select(threadIndex => Task.Run(() =>
                    {
                        int startIndex = (threadIndex * numFrames) / numThreads;
                        int endIndex = ((threadIndex + 1) * numFrames) / numThreads;
                        int localCount = endIndex - startIndex;
                        if (localCount <= 0)
                            return;

                        var framesChunk = new float[localCount, 3, targetHeight, targetWidth];
                        var predictionsChunk = new float[localCount, 2];

                        int frameSize = 3 * targetHeight * targetWidth * sizeof(float);

                        for (int i = startIndex; i < endIndex; i++)
                        {
                            Buffer.BlockCopy(
                                framesArray, i * frameSize,
                                framesChunk, (i - startIndex) * frameSize,
                                frameSize);
                        }

                        positiveness_VideoInference(framesChunk, localCount, predictionsChunk, progressPtr);

                        int predictionSize = 2 * sizeof(float);

                        for (int i = startIndex; i < endIndex; i++)
                        {
                            Buffer.BlockCopy(
                                predictionsChunk, (i - startIndex) * predictionSize,
                                tensorPredictions, i * predictionSize,
                                predictionSize);
                        }
                    }))
                    .ToArray();

                int delay = (ctx != null && ctx.millisecondsDelay > 0)
                    ? ctx.millisecondsDelay
                    : 1000;

                while (!inferenceTasks.All(t => t.IsCompleted))
                {
                    await Task.Delay(delay).ConfigureAwait(true);

                    int currentProgress = Marshal.ReadInt32(progressPtr);
                    float ratio = numFrames > 0
                        ? (float)currentProgress / numFrames
                        : 1.0f;

                    if (ratio < 0) ratio = 0;
                    if (ratio > 1) ratio = 1;

                    int percent = (int)Math.Round(ratio * 100);
                    ctx?.updateProgress?.Invoke(percent, stageName, videoName);
                }

                await Task.WhenAll(inferenceTasks).ConfigureAwait(true);

                ctx?.updateProgress?.Invoke(100, stageName, videoName);
                ctx?.setPositivenessTensorPredictions?.Invoke(tensorPredictions, frameSecInterval);

                string directoryPath = "Output";
                Directory.CreateDirectory(directoryPath);

                string filePath = Path.Combine(directoryPath, $"{videoName}.epp");
                using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                using (var writer = new BinaryWriter(fileStream))
                {
                    writer.Write(numFrames);
                    writer.Write(frameSecInterval);

                    for (int i = 0; i < numFrames; i++)
                    {
                        for (int j = 0; j < 2; j++)
                        {
                            writer.Write(tensorPredictions[i, j]);
                        }
                    }
                }

                Console.WriteLine("Positiveness model complete.");
                string end = DateTime.Now.ToString("HH:mm:ss");
                Console.WriteLine($"Task started at {begin} and ended at {end}");
            }
            finally
            {
                Marshal.FreeHGlobal(progressPtr);
            }
        }

        private static async Task ProcessVideoFilterAsync(
            string videoName,
            int frameSecInterval,
            float[,,,] framesArray,
            int numFrames,
            InferenceContext ctx)
        {
            const string stageName = "Filter";

            if (framesArray == null || numFrames <= 0)
            {
                ctx?.updateProgress?.Invoke(100, stageName, videoName);
                return;
            }

            Console.WriteLine("Running filter model...");
            string begin = DateTime.Now.ToString("HH:mm:ss");

            int targetHeight = framesArray.GetLength(2);
            int targetWidth = framesArray.GetLength(3);
            float[,] tensorPredictions = new float[numFrames, 3];

            IntPtr progressPtr = Marshal.AllocHGlobal(sizeof(int));
            try
            {
                Marshal.WriteInt32(progressPtr, 0);

                int availableCores = Math.Max((int)Math.Floor(Environment.ProcessorCount * CpuUsagePercentage), 1);
                int numThreads = Math.Min(availableCores, numFrames);
                if (numThreads <= 0)
                    numThreads = 1;

                var inferenceTasks = Enumerable.Range(0, numThreads)
                    .Select(threadIndex => Task.Run(() =>
                    {
                        int startIndex = (threadIndex * numFrames) / numThreads;
                        int endIndex = ((threadIndex + 1) * numFrames) / numThreads;
                        int localCount = endIndex - startIndex;
                        if (localCount <= 0)
                            return;

                        var framesChunk = new float[localCount, 3, targetHeight, targetWidth];
                        var predictionsChunk = new float[localCount, 3];

                        int frameSize = 3 * targetHeight * targetWidth * sizeof(float);

                        for (int i = startIndex; i < endIndex; i++)
                        {
                            Buffer.BlockCopy(
                                framesArray, i * frameSize,
                                framesChunk, (i - startIndex) * frameSize,
                                frameSize);
                        }

                        filter_VideoInference(framesChunk, localCount, predictionsChunk, progressPtr);

                        int predictionSize = 3 * sizeof(float);

                        for (int i = startIndex; i < endIndex; i++)
                        {
                            Buffer.BlockCopy(
                                predictionsChunk, (i - startIndex) * predictionSize,
                                tensorPredictions, i * predictionSize,
                                predictionSize);
                        }
                    }))
                    .ToArray();

                int delay = (ctx != null && ctx.millisecondsDelay > 0)
                    ? ctx.millisecondsDelay
                    : 1000;

                while (!inferenceTasks.All(t => t.IsCompleted))
                {
                    await Task.Delay(delay).ConfigureAwait(true);

                    int currentProgress = Marshal.ReadInt32(progressPtr);
                    float ratio = numFrames > 0
                        ? (float)currentProgress / numFrames
                        : 1.0f;

                    if (ratio < 0) ratio = 0;
                    if (ratio > 1) ratio = 1;

                    int percent = (int)Math.Round(ratio * 100);
                    ctx?.updateProgress?.Invoke(percent, stageName, videoName);
                }

                await Task.WhenAll(inferenceTasks).ConfigureAwait(true);

                for (int i = 0; i < numFrames; i++)
                {
                    if (IsFrameMostlyDark(framesArray, i))
                    {
                        tensorPredictions[i, 0] = -1;
                        tensorPredictions[i, 1] = -1;
                        tensorPredictions[i, 2] = -1;
                    }
                }

                ctx?.updateProgress?.Invoke(100, stageName, videoName);

                string directoryPath = "Output";
                Directory.CreateDirectory(directoryPath);

                string filePath = Path.Combine(directoryPath, $"{videoName}.efp");
                using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                using (var writer = new BinaryWriter(fileStream))
                {
                    writer.Write(numFrames);
                    writer.Write(frameSecInterval);

                    for (int i = 0; i < numFrames; i++)
                    {
                        for (int j = 0; j < 3; j++)
                        {
                            writer.Write(tensorPredictions[i, j]);
                        }
                    }
                }

                Console.WriteLine("Filter model complete.");
                string end = DateTime.Now.ToString("HH:mm:ss");
                Console.WriteLine($"Task started at {begin} and ended at {end}");
            }
            finally
            {
                Marshal.FreeHGlobal(progressPtr);
            }
        }

        #endregion

        #region SAMP classification

        private static string GetMpaaRating(double rating)
        {
            if (rating < 0.0796)
                return "G";
            if (rating < 0.216)
                return "PG";
            if (rating < 0.464)
                return "PG-13";

            return "R";
        }

        public static void ClassifyVideo(string videoName, InferenceContext ctx)
        {
            string eppPath = Path.Combine("Output", $"{videoName}.epp");
            string efpPath = Path.Combine("Output", $"{videoName}.efp");

            if (!File.Exists(eppPath) || !File.Exists(efpPath))
            {
                Console.WriteLine($"EPP or EFP file missing for {videoName}");
                return;
            }

            try
            {
                double result = predictSAMP(eppPath, efpPath);

                if (double.IsNaN(result))
                {
                    Console.WriteLine($"Prediction for '{videoName}' returned NaN.");
                    return;
                }

                if (result == 2.0)
                {
                    ctx?.setInterpretedResult?.Invoke("Unsafe");
                }
                else
                {
                    string rating = GetMpaaRating(result);
                    ctx?.setInterpretedResult?.Invoke(rating);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while classifying '{videoName}': {ex.Message}");
            }
        }

        #endregion

        #region Public entry point

        public static async Task Main(string videoFilePath, InferenceContext ctx)
        {
            if (string.IsNullOrWhiteSpace(videoFilePath))
                throw new ArgumentException("Video path cannot be null or empty.", nameof(videoFilePath));

            string videoName = Path.GetFileNameWithoutExtension(videoFilePath);

            ctx?.setInterpretedResult?.Invoke("N/A");

            int numFrames;
            int frameSecInterval;

            float[,,,] framesArrayPositiveness =
                LoadVideoFrames(
                    videoFilePath,
                    out numFrames,
                    out frameSecInterval,
                    targetWidth: 227,
                    targetHeight: 227,
                    isRgbOrder: false,
                    useImageNetMean: true);

            if (framesArrayPositiveness == null || numFrames <= 0)
            {
                ctx?.setPositivenessTensorPredictions?.Invoke(new float[0, 2], frameSecInterval);
                ctx?.updateProgress?.Invoke(100, "Positiveness", videoName);
            }
            else
            {
                await ProcessVideoPositivenessAsync(
                    videoName,
                    frameSecInterval,
                    framesArrayPositiveness,
                    numFrames,
                    ctx);
            }

            framesArrayPositiveness = null;

            float[,,,] framesArrayFilter =
                LoadVideoFrames(
                    videoFilePath,
                    out numFrames,
                    out frameSecInterval,
                    targetWidth: 224,
                    targetHeight: 224,
                    isRgbOrder: true,
                    useImageNetMean: false);

            if (framesArrayFilter == null || numFrames <= 0)
            {
                ctx?.updateProgress?.Invoke(100, "Filter", videoName);
            }
            else
            {
                await ProcessVideoFilterAsync(
                    videoName,
                    frameSecInterval,
                    framesArrayFilter,
                    numFrames,
                    ctx);

                ClassifyVideo(videoName, ctx);
            }

            framesArrayFilter = null;

            Console.WriteLine("File processing completed.\n");
        }

        #endregion
    }
}