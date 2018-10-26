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
        public static StatusHandler statusHandler;
        public static FileParser fileParser;
        public static Process osuProcess = null;

        public MainWindow()
        {
            InitializeComponent();
            InitializeEvents();
        }

        public void InitializeEvents()
        {
            statusHandler = new StatusHandler(this);
            fileParser = new FileParser(this);
            statusHandler.UpdateWindow();
            RelaxButton.Click += RelaxButton_Click;
            AutoPilotButton.Click += AutoPilotButton_Click;

            Key1TextBox.KeyDown += new KeyEventHandler(TextBox_OnKeyPress);
            Key2TextBox.KeyDown += new KeyEventHandler(TextBox_OnKeyPress);
            Key1TextBox.LostFocus += TextBox_OnLostFocus;
            Key2TextBox.LostFocus += TextBox_OnLostFocus;

            new Thread(() =>
            {
                GameState pastStatus = statusHandler.GetGameState();

                while (true)
                {
                    Process[] processes = Process.GetProcessesByName("osu!");
                    osuProcess = processes.Length > 0 ? processes[0] : null;
                    if (pastStatus != statusHandler.UpdateGameState())
                    {
                        if (statusHandler.GetGameState() == GameState.Playing)
                            fileParser.FindFilePath();

                        pastStatus = statusHandler.GetGameState();
                        this.Dispatcher.Invoke(statusHandler.UpdateWindow);
                    }
                    Thread.Sleep(100);
                }
            }).Start();

            void RelaxButton_Click(object sender, RoutedEventArgs e)
            {
                RelaxButton.Content = statusHandler.IsRelaxRunning() ? "Enable Relax" : "Disable Relax";
                statusHandler.ToggleRelax();
                statusHandler.UpdateWindow();
            }

            void AutoPilotButton_Click(object sender, RoutedEventArgs e)
            {
                AutoPilotButton.Content = statusHandler.IsAutoPilotRunning() ? "Enable AutoPilot" : "Disable AutoPilot";
                statusHandler.ToggleAutoPilot();
                statusHandler.UpdateWindow();
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
                statusHandler.SetKey1(Key1TextBox.Text[0]);
                statusHandler.SetKey2(Key2TextBox.Text[0]);
                statusHandler.UpdateWindow();
                MainGrid.Focus();
            }

            void TextBox_OnLostFocus(object sender, EventArgs e)
            {
                Key1TextBox.Text = statusHandler.GetKey1().ToString();
                Key2TextBox.Text = statusHandler.GetKey2().ToString();
            }
        }
        
        private void InputBox_GotFocus(object sender, RoutedEventArgs e)
        {
            ((TextBox)sender).Text = string.Empty;
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            MainGrid.Focus();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            App.Current.Shutdown();
            Process.GetCurrentProcess().Kill();
        }
    }
}
