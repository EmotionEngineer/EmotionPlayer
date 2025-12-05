namespace EmotionPlayer
{
    /// <summary>
    /// Holds inference results for a single video.
    /// </summary>
    public class InferenceResult
    {
        /// <summary>
        /// Raw predictions from the positiveness model.
        /// Shape: [numFrames, numClasses].
        /// </summary>
        internal float[,] tensorPredictions;

        /// <summary>
        /// Interval in seconds between frames used for inference.
        /// This is the same value that is written to *.epp/*.efp files.
        /// </summary>
        internal int frameSecInterval;

        /// <summary>
        /// Interpreted result returned by SAMP model (e.g. "G", "PG-13", "Unsafe").
        /// </summary>
        internal string interpretedResult;
    }
}