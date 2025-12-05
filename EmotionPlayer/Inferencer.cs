using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using OpenCvSharp;

namespace EmotionPlayer
{
    /// <summary>
    /// Static entry point for all heavy-weight video inference logic.
    /// Wraps native DLLs (filter.dll, positiveness.dll, samp.dll)
    /// and provides a higher level async API for the WPF UI.
    /// </summary>
    internal static class Inferencer
    {
        /// <summary>
        /// Fraction of available CPU cores to use.
        /// Used to determine number of parallel chunks.
        /// </summary>
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

        /// <summary>
        /// Returns true if the given frame is mostly dark based on average brightness.
        /// Expects frames normalized to [0..1].
        /// </summary>
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

        /// <summary>
        /// Choose sampling interval (in seconds) based on total video duration.
        /// Longer videos are sampled more sparsely to keep total frame count reasonable.
        /// </summary>
        private static int GetFrameSecInterval(double totalSeconds)
        {
            const int secPerMinute = 60;
            const int secPerHour = secPerMinute * 60;

            if (totalSeconds <= 20 * secPerMinute)
                return 1;   // Up to 20 minutes - every second
            if (totalSeconds <= 1 * secPerHour)
                return 2;   // 20 minutes to 1 hour - every 2 seconds
            if (totalSeconds <= 1.8 * secPerHour)
                return 3;   // 1 to 1.8 hours - every 3 seconds
            if (totalSeconds <= 2.1 * secPerHour)
                return 4;   // 1.8 to 2.1 hours - every 4 seconds
            if (totalSeconds <= 3 * secPerHour)
                return 5;   // 2.1 to 3 hours - every 5 seconds

            return 6;       // More than 3 hours - every 6 seconds
        }

        /// <summary>
        /// Loads and preprocesses video frames into a 4D tensor:
        /// [numFrames, 3, targetHeight, targetWidth].
        /// Handles "weird" FPS values and very short videos robustly.
        /// </summary>
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
                    // Fallback FPS for malformed containers.
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

                // Number of samples across the timeline – ceil to ensure at least one frame.
                numFrames = (int)Math.Ceiling(totalSeconds / frameSecInterval);
                if (numFrames <= 0)
                    numFrames = 1;

                var framesArray = new float[numFrames, 3, targetHeight, targetWidth];

                // BGR mean values (ImageNet).
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
                                        // RGB order with ImageNet mean subtraction.
                                        framesArray[i, 0, y, x] = r - meanValues[2];
                                        framesArray[i, 1, y, x] = g - meanValues[1];
                                        framesArray[i, 2, y, x] = b - meanValues[0];
                                    }
                                    else
                                    {
                                        // BGR order with ImageNet mean subtraction.
                                        framesArray[i, 0, y, x] = b - meanValues[0];
                                        framesArray[i, 1, y, x] = g - meanValues[1];
                                        framesArray[i, 2, y, x] = r - meanValues[2];
                                    }
                                }
                                else
                                {
                                    if (isRgbOrder)
                                    {
                                        // RGB min-max normalization to [0..1].
                                        framesArray[i, 0, y, x] = r / 255.0f;
                                        framesArray[i, 1, y, x] = g / 255.0f;
                                        framesArray[i, 2, y, x] = b / 255.0f;
                                    }
                                    else
                                    {
                                        // BGR min-max normalization to [0..1].
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

        #region Model runners (parallel chunks + shared progress)

        /// <summary>
        /// Runs the positiveness model on a preloaded 4D tensor of frames in parallel chunks.
        /// Produces *.epp file and passes the tensor back via <see cref="InferenceContext"/>.
        /// </summary>
        private static async Task ProcessVideoPositivenessAsync(
            string videoName,
            int frameSecInterval,
            float[,,,] framesArray,
            int numFrames,
            InferenceContext ctx)
        {
            if (framesArray == null || numFrames <= 0)
            {
                ctx?.setPositivenessTensorPredictions?.Invoke(new float[0, 2], frameSecInterval);
                ctx?.updateProgress?.Invoke(100);
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

                bool[] threadCompletion = new bool[numThreads];

                // Run native inference on chunks in parallel.
                var inferenceTasks = Enumerable.Range(0, numThreads)
                    .Select(threadIndex => Task.Run(() =>
                    {
                        try
                        {
                            // Balanced chunk boundaries.
                            int startIndex = (threadIndex * numFrames) / numThreads;
                            int endIndex = ((threadIndex + 1) * numFrames) / numThreads;
                            int localCount = endIndex - startIndex;
                            if (localCount <= 0)
                                return;

                            var framesChunk = new float[localCount, 3, targetHeight, targetWidth];
                            var predictionsChunk = new float[localCount, 2];

                            int frameSize = 3 * targetHeight * targetWidth * sizeof(float);

                            // Copy frames for this chunk.
                            for (int i = startIndex; i < endIndex; i++)
                            {
                                Buffer.BlockCopy(
                                    framesArray, i * frameSize,
                                    framesChunk, (i - startIndex) * frameSize,
                                    frameSize);
                            }

                            // Native call for this chunk.
                            positiveness_VideoInference(framesChunk, localCount, predictionsChunk, progressPtr);

                            int predictionSize = 2 * sizeof(float);

                            // Copy predictions back to full tensor.
                            for (int i = startIndex; i < endIndex; i++)
                            {
                                Buffer.BlockCopy(
                                    predictionsChunk, (i - startIndex) * predictionSize,
                                    tensorPredictions, i * predictionSize,
                                    predictionSize);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Positiveness thread {threadIndex} failed: {ex}");
                        }
                        finally
                        {
                            threadCompletion[threadIndex] = true;
                        }
                    }))
                    .ToArray();

                int delay = (ctx != null && ctx.millisecondsDelay > 0)
                    ? ctx.millisecondsDelay
                    : 1000;

                // Poll shared progress while any thread is still running.
                while (!threadCompletion.All(x => x))
                {
                    await Task.Delay(delay).ConfigureAwait(true);

                    int currentProgress = Marshal.ReadInt32(progressPtr);
                    float ratio = numFrames > 0
                        ? (float)currentProgress / numFrames
                        : 1.0f;

                    if (ratio < 0) ratio = 0;
                    if (ratio > 1) ratio = 1;

                    int percent = (int)Math.Round(ratio * 100);
                    ctx?.updateProgress?.Invoke(percent);
                }

                await Task.WhenAll(inferenceTasks).ConfigureAwait(true);

                // Ensure final 100%.
                ctx?.updateProgress?.Invoke(100);
                ctx?.setPositivenessTensorPredictions?.Invoke(tensorPredictions, frameSecInterval);

                // Serialize results to *.epp
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

        /// <summary>
        /// Runs the filter model on a preloaded 4D tensor of frames in parallel chunks.
        /// Produces *.efp file. Dark frames are marked with -1 in all channels.
        /// </summary>
        private static async Task ProcessVideoFilterAsync(
            string videoName,
            int frameSecInterval,
            float[,,,] framesArray,
            int numFrames,
            InferenceContext ctx)
        {
            if (framesArray == null || numFrames <= 0)
            {
                ctx?.updateProgress?.Invoke(100);
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

                bool[] threadCompletion = new bool[numThreads];

                var inferenceTasks = Enumerable.Range(0, numThreads)
                    .Select(threadIndex => Task.Run(() =>
                    {
                        try
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
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Filter thread {threadIndex} failed: {ex}");
                        }
                        finally
                        {
                            threadCompletion[threadIndex] = true;
                        }
                    }))
                    .ToArray();

                int delay = (ctx != null && ctx.millisecondsDelay > 0)
                    ? ctx.millisecondsDelay
                    : 1000;

                while (!threadCompletion.All(x => x))
                {
                    await Task.Delay(delay).ConfigureAwait(true);

                    int currentProgress = Marshal.ReadInt32(progressPtr);
                    float ratio = numFrames > 0
                        ? (float)currentProgress / numFrames
                        : 1.0f;

                    if (ratio < 0) ratio = 0;
                    if (ratio > 1) ratio = 1;

                    int percent = (int)Math.Round(ratio * 100);
                    ctx?.updateProgress?.Invoke(percent);
                }

                await Task.WhenAll(inferenceTasks).ConfigureAwait(true);

                // Mark dark frames with -1.
                for (int i = 0; i < numFrames; i++)
                {
                    if (IsFrameMostlyDark(framesArray, i))
                    {
                        tensorPredictions[i, 0] = -1;
                        tensorPredictions[i, 1] = -1;
                        tensorPredictions[i, 2] = -1;
                    }
                }

                ctx?.updateProgress?.Invoke(100);

                // Serialize results to *.efp
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

        /// <summary>
        /// Reads *.epp and *.efp files produced by the models
        /// and runs SAMP classifier. Result is propagated through
        /// <see cref="InferenceContext.setInterpretedResult"/>.
        /// </summary>
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

        /// <summary>
        /// High-level async entry point used by the WPF UI.
        /// Performs two passes over the video:
        /// 1) positiveness model (EPP + raw tensor),
        /// 2) filter model (EFP) and SAMP classification.
        /// </summary>
        public static async Task Main(string videoFilePath, InferenceContext ctx)
        {
            if (string.IsNullOrWhiteSpace(videoFilePath))
                throw new ArgumentException("Video path cannot be null or empty.", nameof(videoFilePath));

            string videoName = Path.GetFileNameWithoutExtension(videoFilePath);

            // 1. Positiveness pass.
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

            if (framesArrayPositiveness != null)
            {
                await ProcessVideoPositivenessAsync(
                    videoName,
                    frameSecInterval,
                    framesArrayPositiveness,
                    numFrames,
                    ctx);
            }

            framesArrayPositiveness = null;

            // 2. Filter pass + SAMP classification.
            float[,,,] framesArrayFilter =
                LoadVideoFrames(
                    videoFilePath,
                    out numFrames,
                    out frameSecInterval,
                    targetWidth: 224,
                    targetHeight: 224,
                    isRgbOrder: true,
                    useImageNetMean: false);

            if (framesArrayFilter != null)
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