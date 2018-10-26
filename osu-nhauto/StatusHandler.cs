using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace osu_nhauto {

    public enum GameState
    {
        Playing, Idle, NotOpen
    }

    public class StatusHandler
    {
        MainWindow Main;

        public StatusHandler(MainWindow mw)
        {
            Main = mw;
            autopilotRunning = false;
            relaxRunning = false;
            key1 = 'Z';
            key2 = 'X';
            updateGameState();
        }

        public void updateWindow()
        {
            Main.StatusWindow.Text = string.Empty;
            Main.StatusWindow.Inlines.Add(new Run("osu! Window Status: ") { FontWeight = FontWeights.Bold });
            updateGameState();
            switch (state)
            {
                case GameState.NotOpen:
                    Main.StatusWindow.Inlines.Add(new Run("Not Open") { Foreground = Brushes.Red });
                    break;
                case GameState.Idle:
                    Main.StatusWindow.Inlines.Add(new Run("Idle") { Foreground = Brushes.Gray });
                    break;
                case GameState.Playing:
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

        public GameState updateGameState()
        {
            Process[] osuWindow = Process.GetProcessesByName("osu!");
            if (osuWindow.Length == 0)
            {
                state = GameState.NotOpen;
            }
            else if (osuWindow[0].MainWindowTitle.IndexOf("-", StringComparison.InvariantCulture) > -1)
            {
                state = GameState.Playing;
            }
            else
            {
                state = GameState.Idle;
            }
            return state;
        }

        public GameState GetGameState() => state;
        public void SetGameState(GameState status) => state = status;
        public void ToggleAutoPilot() => this.autopilotRunning = !this.autopilotRunning;
        public void ToggleRelax() => this.relaxRunning = !this.relaxRunning;
        public char GetKey1() => this.key1;
        public char GetKey2() => this.key2;
        public void SetKey1(char key) => this.key1 = key;
        public void SetKey2(char key) => this.key2 = key;
        public bool IsAutoPilotRunning() => this.autopilotRunning;
        public bool IsRelaxRunning() => this.relaxRunning;

        private char key1;
        private char key2;
        private bool autopilotRunning;
        private bool relaxRunning;
        private GameState state;
    }
}
