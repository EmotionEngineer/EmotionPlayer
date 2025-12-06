using System;

namespace EmotionPlayer
{
    /// <summary>
    /// Context object used to connect the inference backend with the UI.
    /// </summary>
    internal class InferenceContext
    {
        /// <summary>
        /// Delay in milliseconds between progress polling iterations.
        /// </summary>
        internal int millisecondsDelay;

        /// <summary>
        /// Callback to report progress: (percentage, stage, videoName).
        /// stage: e.g. "Positiveness", "Filter".
        /// videoName: typically the file name without extension.
        /// </summary>
        internal Action<int, string, string> updateProgress;

        /// <summary>
        /// Callback to set interpreted final result (e.g. MPAA rating or "Unsafe").
        /// </summary>
        internal Action<string> setInterpretedResult;

        /// <summary>
        /// Callback to provide raw positiveness predictions tensor and
        /// the sampling interval in seconds between frames used for inference.
        /// </summary>
        internal Action<float[,], int> setPositivenessTensorPredictions;
    }
}