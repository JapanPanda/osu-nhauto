using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace osu_nhauto {

    public enum GameState
    {
        PLAYING, IDLE, NOT_OPEN
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

        public void updateWindow(FileParser fileParser)
        {
            Main.StatusWindow.Text = string.Empty;
            Main.StatusWindow.Inlines.Add(new Run("osu! Window Status: ") { FontWeight = FontWeights.Bold });
            updateGameState();
            switch (state)
            {
                case GameState.NOT_OPEN:
                    Main.StatusWindow.Inlines.Add(new Run("Not Open") { Foreground = Brushes.Red });
                    break;
                case GameState.IDLE:
                    Main.StatusWindow.Inlines.Add(new Run("Idle") { Foreground = Brushes.Gray });
                    break;
                case GameState.PLAYING:
                    Main.StatusWindow.Inlines.Add(new Run("Playing") { Foreground = Brushes.Green });
                    break;
                default:
                    Main.StatusWindow.Inlines.Add(new Run("UNKNOWN ? ? ?") { Foreground = Brushes.DarkRed });
                    break;
            }
            Main.StatusWindow.Inlines.Add("\n");
            Main.StatusWindow.Inlines.Add(new Run("Beatmap Loaded: ") { FontWeight = FontWeights.Bold });
            if (state != GameState.PLAYING)
            {
                Main.StatusWindow.Inlines.Add(new Run("None") { Foreground = Brushes.Gray });
            }
            else
            {
                string fileName = fileParser.getFileName();
                Console.WriteLine(fileName);
                if (fileName == "Duplicate Folders Found")
                {
                    Main.StatusWindow.Inlines.Add(new Run("Duplicate Song Folders Found") { Foreground = Brushes.Red });
                }
                else if (fileName == "Duplicate .osu Files Found")
                {
                    Main.StatusWindow.Inlines.Add(new Run("Duplicate .osu Files Found") { Foreground = Brushes.Red });
                }
                else if (fileName != null)
                {
                    Main.StatusWindow.Inlines.Add(new Run(fileName) { Foreground = Brushes.Green });
                }
                else
                {
                    Main.StatusWindow.Inlines.Add(new Run("Retry the map to initialize") { Foreground = Brushes.Green });
                }
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
                state = GameState.NOT_OPEN;
            }
            else if (osuWindow[0].MainWindowTitle.IndexOf("-", StringComparison.InvariantCulture) > -1)
            {
                state = GameState.PLAYING;
            }
            else
            {
                state = GameState.IDLE;
            }
            return state;
        }

        public GameState getGameState() => state;
        public void setGameState(GameState status) => state = status;
        public void toggleAutoPilot() => this.autopilotRunning = !this.autopilotRunning;
        public void toggleRelax() => this.relaxRunning = !this.relaxRunning;
        public void setKey1(char key) => this.key1 = key;
        public void setKey2(char key) => this.key2 = key;
        public bool isAutoPilotRunning() => this.autopilotRunning;
        public bool isRelaxRunning() => this.relaxRunning;

        private char key1;
        private char key2;
        private bool autopilotRunning;
        private bool relaxRunning;
        private GameState state;
    }
}
