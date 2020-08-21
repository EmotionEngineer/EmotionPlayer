using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Media.TextFormatting;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Shell;
using EmotionPlayer;


namespace EmotionPlayer
{
    public partial class App : System.Windows.Application
    {
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "4.0.0.0")]
        public void InitializeComponent()
        {

#line 5 "..\..\App.xaml"
            this.Startup += new System.Windows.StartupEventHandler(this.Application_Startup);

#line default
#line hidden
        }
        [System.STAThreadAttribute()]
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [System.CodeDom.Compiler.GeneratedCodeAttribute("PresentationBuildTasks", "4.0.0.0")]
        public static void Main()
        {
            EmotionPlayer.App app = new EmotionPlayer.App();
            app.InitializeComponent();
            app.Run();
        }
    }
}
