using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using OpenCvSharp;

namespace EmotionPlayer
{
    public partial class FileWindow : System.Windows.Window
    {
        private ProgressBarWindow pbw = null;
        private readonly List<InferenceResult> data;

        private string filter;

        public FileWindow(IEnumerable<string> sources, List<InferenceResult> data, string filter = "Все файлы|*.*")
        {
            InitializeComponent();

            this.data = data;
            this.filter = filter;
            list.Items.AddRange(sources);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Close(false);
        }
        private async void Button_Click_1(object sender, RoutedEventArgs e)
        {
            data.Clear();
            await ProcessVideosAsync();
        }
        void worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            pbw.UpdateProgress(e.ProgressPercentage);
        }
        private async Task ProcessVideosAsync()
        {
            int numVideo = 0;
            while (numVideo < list.Items.Count)
            {
                pbw = new ProgressBarWindow();
                pbw.Show();
                await ProcessVideoAsync(list.Items[numVideo].ToString());
                pbw.Close();
                numVideo++;
            }
            Close(true);
        }
        private async Task ProcessVideoAsync(string videoFilePath)
        {
            InferenceContext ctx = new InferenceContext();
            InferenceResult result = new InferenceResult();
            ctx.millisecondsDelay = 1000;
            ctx.setPositivenessTensorPredictions = x =>
            {
                result.tensorPredictions = x;
            };
            ctx.updateProgress = pbw.UpdateProgress;
            ctx.setInterpretedResult = x =>
            {
                result.interpretedResult = x;
            };
            await Inferencer.Main(videoFilePath, ctx);
            data.Add(result);
        }
        private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }
        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            OpenSourcesDialog();
        }
        private void list_MouseDown(object sender, MouseButtonEventArgs e)
        {
            list.RemoveSelection();
        }
        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            list.Items.TryRemoveAt(list.SelectedIndex);
        }
        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            list.Items.Clear();
        }
        private void Button_Click_5(object sender, RoutedEventArgs e)
        {
            list.TryMoveSelected(list.SelectedIndex + 1);
        }
        private void Button_Click_6(object sender, RoutedEventArgs e)
        {
            list.TryMoveSelected(list.SelectedIndex - 1);
        }
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case (Key.S): Close(true); break;
                case (Key.Escape): Close(false); break;
                case (Key.A):
                case (Key.OemPlus): OpenSourcesDialog(); break;
                case (Key.D):
                case (Key.OemMinus): list.Items.TryRemoveAt(list.SelectedIndex); break;
                case (Key.F): list.RemoveSelection(); break;
                case (Key.C): list.Items.Clear(); break;
                case (Key.N): list.TryMoveSelected(list.SelectedIndex + 1); break;
                case (Key.U): list.TryMoveSelected(list.SelectedIndex - 1); break;
            }
        }
    }
}