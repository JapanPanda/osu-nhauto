using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace osu_nhauto
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static StatusHandler statusHandler;
        public static FileParser fileParser;
        public static string currentBeatmapPath = null;
        public static Osu osu = null;

        public static readonly object lockObject = new object();

        public MainWindow()
        {
            InitializeComponent();
            InitializeEvents();
        }

        public void InitializeEvents()
        {
            osu = new Osu();
            player = new Player(osu);

            statusHandler = new StatusHandler(this);
            fileParser = new FileParser(this);

            statusHandler.UpdateWindow();
            RelaxButton.Click += RelaxButton_Click;
            AutoPilotButton.Click += AutoPilotButton_Click;

            Key1TextBox.KeyDown += new KeyEventHandler(TextBox_OnKeyPress);
            Key2TextBox.KeyDown += new KeyEventHandler(TextBox_OnKeyPress);
            Key1TextBox.LostFocus += TextBox_OnLostFocus;
            Key2TextBox.LostFocus += TextBox_OnLostFocus;

            
            Thread playerUpdateThread = new Thread(player.Update);
            new Thread(() =>
            {
                GameState pastStatus = statusHandler.GetGameState();
                while (true)
                {
                    if (pastStatus != statusHandler.UpdateGameState())
                    {
                        if (statusHandler.GetGameState() == GameState.Playing)
                        {
                            CurrentBeatmap beatmap = new CurrentBeatmap();
                            currentBeatmapPath = beatmap.Get();
                            if (currentBeatmapPath != null)
                            {
                                beatmap.Parse();
                                player.SetBeatmap(beatmap);

                                this.Dispatcher.Invoke(new Thread(player.Update).Start);
                            }
                        }

                        pastStatus = statusHandler.GetGameState();
                        this.Dispatcher.Invoke(statusHandler.UpdateWindow);
                    }
                    if (pastStatus == GameState.Loading)
                    {
                        if (statusHandler.UpdateGameState() != GameState.Loading) {
                            pastStatus = statusHandler.GetGameState();
                            statusHandler.UpdateWindow();
                        }
                    }

                    Thread.Sleep(1000);
                }
            }).Start();

            void RelaxButton_Click(object sender, RoutedEventArgs e)
            {
                RelaxButton.Content = player.IsRelaxRunning() ? "Enable Relax" : "Disable Relax";
                player.ToggleRelax();
                statusHandler.UpdateWindow();
            }

            void AutoPilotButton_Click(object sender, RoutedEventArgs e)
            {
                AutoPilotButton.Content = player.IsAutoPilotRunning() ? "Enable AutoPilot" : "Disable AutoPilot";
                player.ToggleAutoPilot();
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
                player.SetKey1(Key1TextBox.Text[0]);
                player.SetKey2(Key2TextBox.Text[0]);
                statusHandler.UpdateWindow();
                MainGrid.Focus();
            }

            void TextBox_OnLostFocus(object sender, EventArgs e)
            {
                Key1TextBox.Text = player.GetKey1().ToString();
                Key2TextBox.Text = player.GetKey2().ToString();
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

        public Player GetPlayer() => this.player;
        private Player player;
    }
}
