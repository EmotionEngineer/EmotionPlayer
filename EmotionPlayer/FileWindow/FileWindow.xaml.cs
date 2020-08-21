using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace EmotionPlayer
{
    public partial class FileWindow : Window
    {
        private BackgroundWorker worker = new BackgroundWorker();
        private ProgressBarWindow pbw = null;
        public static List<float[]> data = new List<float[]>();
        private int gc = 0;
        [StructLayout(LayoutKind.Sequential)]
        public struct progress
        {
            public int cur;
            public int end;
        };
        [DllImport("EmotionLib.dll")]
        public static extern IntPtr make([MarshalAs(UnmanagedType.LPStr)] string a, double fps, ref progress bar);
        [DllImport("EmotionLib.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int clean(IntPtr ptr);
        private string filter;

        public FileWindow(IEnumerable<string> sources, string filter = "Все файлы|*.*")
        {
            InitializeComponent();

            this.filter = filter;
            list.Items.AddRange(sources);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Close(false);
        }
        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            data.Clear();
            for (int z = 0; z < list.Items.Count; z++) ShowProgress();
        }
        void worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            pbw.UpdateProgress(e.ProgressPercentage);
        }
        void ShowProgress()
        {
            try
            {
                worker.WorkerReportsProgress = true;
                worker.DoWork += ProcessLogsAsynch;
                worker.ProgressChanged += worker_ProgressChanged;
                while (!worker.IsBusy)
                {
                    worker.RunWorkerAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "ERROR");
            }
        }
        private void ProcessLogsAsynch(object sender, DoWorkEventArgs e)
        {
            float rev = 0;
            var bar = new progress();
            bar.cur = 0; bar.end = 100;
            string begin = DateTime.Now.ToString("h:mm:ss tt");
            float[] result = null;
            int arrayLength = 0;
            Dispatcher.Invoke(() =>
            {
                new Thread(() =>
                {
                    Thread.CurrentThread.IsBackground = true;
                    IntPtr ptr = make(list.Items[gc].ToString(), 0, ref bar);
                    byte[] ba = new byte[sizeof(float)];
                    for (int i = 0; i < ba.Length; i++)
                        ba[i] = Marshal.ReadByte(ptr, i);
                    float v = BitConverter.ToSingle(ba, 0);
                    arrayLength = (int)v;
                    IntPtr start = IntPtr.Add(ptr, 4);
                    result = new float[arrayLength];
                    Marshal.Copy(start, result, 0, arrayLength);
                    clean(ptr);
                    data.Add(result);
                }).Start();

                pbw = new ProgressBarWindow();
                pbw.Show();
            });

            while (rev != 1)
            {
                Thread.Sleep(100);
                rev = (float)bar.cur / (float)bar.end;
                (sender as BackgroundWorker).ReportProgress((int)Math.Round(rev * 100));
            }
            
            Dispatcher.Invoke(() =>
            {
                if (gc == list.Items.Count-1) Close(true);
                gc++;
            });
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
