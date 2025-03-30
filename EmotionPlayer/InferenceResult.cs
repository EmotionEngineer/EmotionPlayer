using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EmotionPlayer
{
    public class InferenceResult
    {
        internal float[,] tensorPredictions;
        internal string interpretedResult;
    }
}
