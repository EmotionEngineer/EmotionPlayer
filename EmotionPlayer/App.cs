using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.IO.MemoryMappedFiles;
using System.IO;
using System.Text;

namespace EmotionPlayer
{
    public partial class App : Application
    {
        public const long ARGS_SIZE = 5_000_000;
        public const string MEMORY_NAME = "EmotionPlayer_0x24";

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException += (o, ex) => MessageBox.Show(ex.ExceptionObject.ToString(), "Unhandled exception", MessageBoxButton.OK, MessageBoxImage.Error);
            new MainWindow().Show();
        }
    }
}
