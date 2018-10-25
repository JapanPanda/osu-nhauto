using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace osu_nhauto
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        internal static MainWindow Main;

        class StatusHandler {


            public StatusHandler()
            {
                autopilotRunning = false;
                relaxRunning = false;
            }

            public void updateWindow()
            {
            
                Main.statusWindow.Inlines.Add(new Run("AutoPilot: ") { FontWeight = FontWeights.Bold });
                if (autopilotRunning)
                {
                    Main.statusWindow.Inlines.Add(new Run("Running") { Foreground = Brushes.Green });
                }
                else
                {
                    Main.statusWindow.Inlines.Add(new Run("Not Running") { Foreground = Brushes.Red });
                }
                Main.statusWindow.Inlines.Add("\n");
                Main.statusWindow.Inlines.Add(new Run("Relax: ") { FontWeight = FontWeights.Bold });
                if (relaxRunning)
                {
                    Main.statusWindow.Inlines.Add(new Run("Running") { Foreground = Brushes.Green });
                }
                else
                {
                    Main.statusWindow.Inlines.Add(new Run("Not Running") { Foreground = Brushes.Red });
                }
            }

            public bool isAutoPilotRunning() { return this.autopilotRunning; }
            public bool isRelaxRunning(){ return this.relaxRunning; }    
            private bool autopilotRunning;
            private bool relaxRunning;
        }

        public void main()
        {
            // status is name of status window text block
            StatusHandler status = new StatusHandler();

            status.updateWindow();

        }

        public MainWindow()
        {
            Main = this;
            InitializeComponent();

            main();
        }
    }
}
