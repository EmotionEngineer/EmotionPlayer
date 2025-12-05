using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace EmotionPlayer
{
    /// <summary>
    /// Window that allows the user to manage the list of video files and
    /// triggers inference for each selected file.
    /// </summary>
    public partial class FileWindow : Window
    {
        private ProgressBarWindow pbw;
        private readonly List<InferenceResult> data;
        private readonly string filter;

        public FileWindow(IEnumerable<string> sources, List<InferenceResult> data, string filter = "Все файлы|*.*")
        {
            InitializeComponent();

            this.data = data ?? throw new ArgumentNullException(nameof(data));
            this.filter = filter ?? "Все файлы|*.*";

            // Add initial sources (if any) to the list box.
            list.Items.AddRange(sources ?? Enumerable.Empty<string>());
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            // Cancel
            Close(false);
        }

        private async void Button_Click_1(object sender, RoutedEventArgs e)
        {
            // Recognize
            data.Clear();
            await ProcessVideosAsync();
        }

        /// <summary>
        /// Processes all videos currently listed in the UI sequentially.
        /// Shows a progress window for each video.
        /// </summary>
        private async Task ProcessVideosAsync()
        {
            for (int i = 0; i < list.Items.Count; i++)
            {
                string path = list.Items[i]?.ToString();
                if (string.IsNullOrWhiteSpace(path))
                    continue;

                pbw = new ProgressBarWindow();
                pbw.Show();

                await ProcessVideoAsync(path);

                pbw.Close();
                pbw = null;
            }

            // Finish with DialogResult = true so caller knows recognition completed.
            Close(true);
        }

        /// <summary>
        /// Runs the inference pipeline for a single video and
        /// collects results into the shared <see cref="data"/> list.
        /// </summary>
        private async Task ProcessVideoAsync(string videoFilePath)
        {
            var ctx = new InferenceContext
            {
                // How often we poll the native progress indicator.
                millisecondsDelay = 1000,

                // Progress is reported to the dedicated progress window.
                updateProgress = percentage => pbw?.UpdateProgress(percentage)
            };

            var result = new InferenceResult();

            // Capture positiveness tensor AND sampling interval.
            ctx.setPositivenessTensorPredictions = (tensor, interval) =>
            {
                result.tensorPredictions = tensor;
                result.frameSecInterval = interval;
            };

            string interpretedResult = null;

            // Capture interpreted result (MPAA rating or "Unsafe").
            ctx.setInterpretedResult = rating => interpretedResult = rating;

            await Inferencer.Main(videoFilePath, ctx);

            // Store final interpreted result.
            result.interpretedResult = interpretedResult;

            data.Add(result);
        }

        private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            // Add files
            OpenSourcesDialog();
        }

        private void list_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Deselect item on background click
            list.RemoveSelection();
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            // Remove selected
            list.Items.TryRemoveAt(list.SelectedIndex);
        }

        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            // Clear all
            list.Items.Clear();
        }

        private void Button_Click_5(object sender, RoutedEventArgs e)
        {
            // Move down
            list.TryMoveSelected(list.SelectedIndex + 1);
        }

        private void Button_Click_6(object sender, RoutedEventArgs e)
        {
            // Move up
            list.TryMoveSelected(list.SelectedIndex - 1);
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.S:
                    Close(true);
                    break;
                case Key.Escape:
                    Close(false);
                    break;

                case Key.A:
                case Key.OemPlus:
                    OpenSourcesDialog();
                    break;

                case Key.D:
                case Key.OemMinus:
                    list.Items.TryRemoveAt(list.SelectedIndex);
                    break;

                case Key.F:
                    list.RemoveSelection();
                    break;

                case Key.C:
                    list.Items.Clear();
                    break;

                case Key.N:
                    list.TryMoveSelected(list.SelectedIndex + 1);
                    break;

                case Key.U:
                    list.TryMoveSelected(list.SelectedIndex - 1);
                    break;
            }
        }
    }
}