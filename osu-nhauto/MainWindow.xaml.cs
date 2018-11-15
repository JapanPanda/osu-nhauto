using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;

namespace osu_nhauto
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vlc);
        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;

            public POINT(int x, int y)
            {
                this.X = x;
                this.Y = y;
            }
        }


        public static StatusHandler statusHandler;
        public static FileParser fileParser;
        public static string currentBeatmapPath = null;
        public static Osu osu = null;
        public static IntPtr hWnd;
        private HwndSource source = null;
        public static readonly object lockObject = new object();


        public MainWindow()
        {
            InitializeComponent();
            //Loaded += OnLoaded;
            InitializeEvents();
        }

        private void OnLoaded(object sender, RoutedEventArgs routedEventArgs)
        {
            hWnd = new WindowInteropHelper(Application.Current.MainWindow).Handle;
            source = HwndSource.FromHwnd(hWnd);
            source.AddHook(HwndHook);

            if (RegisterHotKey(MainWindow.hWnd, 1, 0, (int)WindowsInput.Native.VirtualKeyCode.VK_F) == false)
            {
                Console.WriteLine(MainWindow.hWnd);
                Console.WriteLine("fail");
            }
            else
            {
                Console.WriteLine(MainWindow.hWnd);
                Console.WriteLine("got it");
            }
        }

        public void InitializeEvents()
        {
            osu = new Osu();
            player = new Player();

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
                Thread playerUpdate = new Thread(player.Initialize);
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

                                if (playerUpdate.ThreadState == System.Threading.ThreadState.Unstarted)
                                    this.Dispatcher.Invoke(playerUpdate.Start);
                            }
                        }
                        else if (statusHandler.GetGameState() == GameState.Idle)
                        {
                            playerUpdate.Abort();
                            playerUpdate = new Thread(player.Initialize);
                        }

                        pastStatus = statusHandler.GetGameState();
                        this.Dispatcher.Invoke(statusHandler.UpdateWindow);
                    }
                    if (pastStatus == GameState.Loading)
                    {
                        if (statusHandler.UpdateGameState() != GameState.Loading) {
                            pastStatus = statusHandler.GetGameState();
                            this.Dispatcher.Invoke(statusHandler.UpdateWindow);
                        }
                    }

                    Thread.Sleep(50);
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

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            switch (msg)
            {
                case WM_HOTKEY:
                    switch (wParam.ToInt32())
                    {
                        case 1:
                            int vkey = (((int)lParam >> 16) & 0xFFFF);
                            Console.WriteLine("got em");
                            POINT p;
                            GetCursorPos(out p);
                            Console.WriteLine("POINT: {0} x {1}", p.X, p.Y);
                            handled = true;
                            break;
                    }
                    break;
            }
            return IntPtr.Zero;
        }

        public Player GetPlayer() => this.player;
        private Player player;
    }
}
