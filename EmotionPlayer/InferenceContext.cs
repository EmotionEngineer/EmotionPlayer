using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EmotionPlayer
{
    internal class InferenceContext
    {
        internal int millisecondsDelay;
        internal Action<int> updateProgress;
        internal Action<string> setInterpretedResult;
        internal Action<float[,]> setPositivenessTensorPredictions;
    }
}
