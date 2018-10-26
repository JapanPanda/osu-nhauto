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
        static StatusHandler statusHandler;

        public void InitializeEvents()
        {
            statusHandler = new StatusHandler(this);
            statusHandler.updateWindow();
            RelaxButton.Click += RelaxButton_Click;
            AutoPilotButton.Click += AutoPilotButton_Click;

            Key1TextBox.KeyDown += new KeyEventHandler(TextBox_OnKeyPress);
            Key2TextBox.KeyDown += new KeyEventHandler(TextBox_OnKeyPress);

            Key1TextBox.LostFocus += TextBox_OnLostFocus;
            Key2TextBox.LostFocus += TextBox_OnLostFocus;

            new Thread(() =>
            {
                GameState pastStatus = statusHandler.updateGameState();
                while (true)
                {
                    if (pastStatus != statusHandler.updateGameState())
                    {
                        this.Dispatcher.Invoke(() =>
                        {
                            statusHandler.updateWindow();
                            pastStatus = statusHandler.getGameState();
                        });
                    }
                    Thread.Sleep(100);
                }
            }).Start();

            void RelaxButton_Click(object sender, RoutedEventArgs e)
            {
                RelaxButton.Content = statusHandler.isRelaxRunning() ? "Enable Relax" : "Disable Relax";
                statusHandler.toggleRelax();
                statusHandler.updateWindow();
            }

            void AutoPilotButton_Click(object sender, RoutedEventArgs e)
            {
                AutoPilotButton.Content = statusHandler.isAutoPilotRunning() ? "Enable AutoPilot" : "Disable AutoPilot";
                statusHandler.toggleAutoPilot();
                statusHandler.updateWindow();
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
                statusHandler.setKey1(Key1TextBox.Text[0]);
                statusHandler.setKey2(Key2TextBox.Text[0]);
                statusHandler.updateWindow();
                MainGrid.Focus();
            }

            void TextBox_OnLostFocus(object sender, EventArgs e)
            {
                Key1TextBox.Text = statusHandler.getKey1().ToString();
                Key2TextBox.Text = statusHandler.getKey2().ToString();
            }
        }
        
        public MainWindow()
        {
            InitializeComponent();
            InitializeEvents();
        }
        
        private void InputBox_GotFocus(object sender, RoutedEventArgs e)
        {
            TextBox text = (TextBox)sender;
            text.Text = string.Empty;
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            MainGrid.Focus();
        }
    }
}
