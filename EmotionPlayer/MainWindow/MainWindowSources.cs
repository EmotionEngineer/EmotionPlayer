
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;

namespace EmotionPlayer
{
    public partial class MainWindow
    {
        public event Action onSourcesChanged;

        private List<string> sources = new List<string>();

        public void TryAddSource(string src)
        {
            if (File.Exists(src))
            {
                sources.Add(src);
                onSourcesChanged?.Invoke();
            }
        }
        public void TryAddSources(IEnumerable<string> sources)
        {
            foreach (string src in sources)
                TryAddSource(src);
        }
        public void TryRemoveSource(string src)
        {
            if (sources.Remove(src))
                onSourcesChanged?.Invoke();
        }

        private void OnSourcesChanged()
        {
            onSourcesChanged -= PlayNext;

            if (sources.Count == 0)
                onSourcesChanged += PlayNext;
        }
    }
}
