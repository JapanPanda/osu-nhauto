using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
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

        const int PROCESS_WM_READ = 0x0010;

        static MainWindow Main;

        class FileParser
        {
            public FileParser()
            {

            }

            public string filePath;
        }

        public enum STATUS
        {
            PLAYING, IDLE, NOT_OPEN
        }

        class StatusHandler
        {

            public StatusHandler()
            {
                autopilotRunning = false;
                relaxRunning = false;
                key1 = 'Z';
                key2 = 'X';
                getWindowStatus();
            }

            public void updateWindow()
            {
                Main.StatusWindow.Text = string.Empty;
                Main.StatusWindow.Inlines.Add(new Run("osu! Window Status: ") { FontWeight = FontWeights.Bold });
                getWindowStatus();
                switch (playing)
                {
                    case STATUS.NOT_OPEN:
                        Main.StatusWindow.Inlines.Add(new Run("Not Open") { Foreground = Brushes.Red });
                        break;
                    case STATUS.IDLE:
                        Main.StatusWindow.Inlines.Add(new Run("Idle") { Foreground = Brushes.Gray });
                        break;
                    case STATUS.PLAYING:
                        Main.StatusWindow.Inlines.Add(new Run("Playing") { Foreground = Brushes.Green });
                        break;
                    default:
                        Main.StatusWindow.Inlines.Add(new Run("UNKNOWN ? ? ?") { Foreground = Brushes.DarkRed });
                        break;
                }
                Main.StatusWindow.Inlines.Add("\n");
                Main.StatusWindow.Inlines.Add(new Run("AutoPilot: ") { FontWeight = FontWeights.Bold });
                if (autopilotRunning)
                {
                    Main.StatusWindow.Inlines.Add(new Run("Running") { Foreground = Brushes.Green });
                }
                else
                {
                    Main.StatusWindow.Inlines.Add(new Run("Not Running") { Foreground = Brushes.Red });
                }
                Main.StatusWindow.Inlines.Add("\n");
                Main.StatusWindow.Inlines.Add(new Run("Relax: ") { FontWeight = FontWeights.Bold });
                if (relaxRunning)
                {
                    Main.StatusWindow.Inlines.Add(new Run("Running") { Foreground = Brushes.Green });
                }
                else
                {
                    Main.StatusWindow.Inlines.Add(new Run("Not Running") { Foreground = Brushes.Red });
                }
                Main.StatusWindow.Inlines.Add("\n");
                Main.StatusWindow.Inlines.Add(new Run("Key 1: ") { FontWeight = FontWeights.Bold });
                Main.StatusWindow.Inlines.Add(this.key1.ToString());
                Main.StatusWindow.Inlines.Add(" ");
                Main.StatusWindow.Inlines.Add(new Run("Key 2: ") { FontWeight = FontWeights.Bold });
                Main.StatusWindow.Inlines.Add(this.key2.ToString());

            }

            public STATUS getWindowStatus()
            {
                Process osuWindow = Process.GetProcessesByName("osu!")[0];
                if (osuWindow == null)
                {
                    playing = STATUS.NOT_OPEN;
                    return playing;
                }
                else if (osuWindow.MainWindowTitle.IndexOf("-", StringComparison.InvariantCulture) > -1)
                {
                    playing = STATUS.PLAYING;
                    return playing;
                }
                else
                {
                    playing = STATUS.IDLE;
                    return playing;
                }
            }

            public STATUS getCurrStatus()
            {
                return playing;
            }

            public void toggleAutoPilot() { this.autopilotRunning = !this.autopilotRunning; }
            public void toggleRelax() { this.relaxRunning = !this.relaxRunning; }
            public void setKey1(char key) { this.key1 = key; }
            public void setKey2(char key) { this.key2 = key; }
            public bool isAutoPilotRunning() { return this.autopilotRunning; }
            public bool isRelaxRunning() { return this.relaxRunning; }
            public void setPlaying(STATUS status) { playing = status; }

            private char key1;
            private char key2;
            private bool autopilotRunning;
            private bool relaxRunning;
            private STATUS playing;
        }

        public void InitializeEvents()
        {

            // status is name of status window text block
            status.updateWindow();
            RelaxButton.Click += RelaxButton_Click;
            AutoPilotButton.Click += AutoPilotButton_Click;

            Key1TextBox.KeyDown += new KeyEventHandler(TextBox_OnKeyPress);
            Key2TextBox.KeyDown += new KeyEventHandler(TextBox_OnKeyPress);

            void RelaxButton_Click(object sender, RoutedEventArgs e)
            {
                RelaxButton.Content = status.isRelaxRunning() ? "Enable Relax" : "Disable Relax";
                status.toggleRelax();
                status.updateWindow();
            }

            void AutoPilotButton_Click(object sender, RoutedEventArgs e)
            {
                AutoPilotButton.Content = status.isAutoPilotRunning() ? "Enable AutoPilot" : "Disable AutoPilot";
                status.toggleAutoPilot();
                status.updateWindow();
            }

            void TextBox_OnKeyPress(object sender, KeyEventArgs e)
            {
                TextBox txtBox = (TextBox)sender;
                string key = e.Key.ToString();

                if (Regex.IsMatch(key, "^D[0-9]"))
                {
                    key = key.Substring(1);
                }
                else if (key.Length > 1)
                {
                    MessageBox.Show("Invalid input. Alphanumeric input only.", "Error!", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                txtBox.Text = key.ToUpper();
                status.setKey1(Main.Key1TextBox.Text[0]);
                status.setKey2(Main.Key2TextBox.Text[0]);
                status.updateWindow();
            }
        }
        
        public MainWindow()
        {
            Main = this;
            InitializeComponent();
         
            main();

            new Thread(() =>
            {
                STATUS pastStatus = status.getWindowStatus();
                while (true)
                {
                    status.getWindowStatus();
                    if (pastStatus != status.getCurrStatus())
                    {
                        this.Dispatcher.Invoke(() =>
                        {
                            status.updateWindow();
                        });
                    }
                    Thread.Sleep(100);
                }
            }).Start();

        }
        
        private void InputBox_GotFocus(object sender, RoutedEventArgs e)
        {
            TextBox text = (TextBox)sender;
            text.Text = string.Empty;
        }

        public class CheckerThread
        {
            public CheckerThread()
            {
                abort = false;
            }
            
            public void startChecking()
            {
                if (abort)
                {
                    Console.WriteLine("Already checking");
                    return;
                }
                windowChecker = new System.Threading.Thread(checkWindow);
                windowChecker.IsBackground = true;
                windowChecker.Start();
            }

            public void stopChecking()
            {
                abort = true;
                if (windowChecker.Join(200) == false)
                {
                    windowChecker.Abort();
                }
                windowChecker = null;
            }

            public void checkWindow()
            {
                while (!abort)
                {
                    Process osuWindow = Process.GetProcessesByName("osu!")[0];
                    if (osuWindow == null)
                    {
                        playing = STATUS.NOT_OPEN;
                    }
                    else if (osuWindow.MainWindowTitle.IndexOf("-", StringComparison.InvariantCulture) > -1)
                    {
                        playing = STATUS.PLAYING;
                    }
                    else
                    {
                        playing = STATUS.IDLE;
                    }
                    Thread.Sleep(100);
                }
            }

            public STATUS getStatus() { return playing; }
            
            System.Threading.Thread windowChecker;
            private STATUS playing;
            private bool abort;
        }
    }

    


}
