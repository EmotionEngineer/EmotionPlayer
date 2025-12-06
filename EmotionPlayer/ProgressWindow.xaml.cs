using System.Windows;
using System.Windows.Input;

namespace EmotionPlayer
{
    /// <summary>
    /// Simple window that shows progress for the current video and stage.
    /// </summary>
    public partial class ProgressBarWindow : Window
    {
        public ProgressBarWindow()
        {
            InitializeComponent();
        }

        private void Progress_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        /// <summary>
        /// Sets the display name of the current video and optional tooltip.
        /// Should be called once per video before inference starts.
        /// </summary>
        /// <param name="videoDisplayName">Name to show (e.g. file name).</param>
        /// <param name="fullPath">Full path for tooltip (optional).</param>
        public void SetVideoName(string videoDisplayName, string fullPath = null)
        {
            if (string.IsNullOrWhiteSpace(videoDisplayName))
            {
                tbVideo.Text = "Video: (unknown)";
                tbVideo.ToolTip = null;
            }
            else
            {
                tbVideo.Text = $"Video: {videoDisplayName}";
                tbVideo.ToolTip = string.IsNullOrWhiteSpace(fullPath)
                    ? videoDisplayName
                    : fullPath;
            }
        }

        /// <summary>
        /// Updates progress bar, stage label and percentage text.
        /// </summary>
        /// <param name="percentage">0..100.</param>
        /// <param name="stage">Logical stage: "Positiveness" / "Filter".</param>
        /// <param name="videoName">Video name, currently not used (reserved for future).</param>
        public void UpdateProgress(int percentage, string stage, string videoName)
        {
            if (percentage < 0) percentage = 0;
            if (percentage > 100) percentage = 100;

            pbLoad.Value = percentage;
            tbPercent.Text = $"{percentage:0}%";

            if (!string.IsNullOrWhiteSpace(stage))
                tbStage.Text = $"Stage: {stage}";
            else
                tbStage.Text = string.Empty;
        }
    }
}