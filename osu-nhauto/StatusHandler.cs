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
        }

        public void UpdateWindow()
        {
            Main.StatusWindow.Text = string.Empty;
            Main.StatusWindow.Inlines.Add(new Run("osu! Window Status: ") { FontWeight = FontWeights.Bold });
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
            Main.StatusWindow.Inlines.Add(new Run("Beatmap Loaded: ") { FontWeight = FontWeights.Bold });
            if (state != GameState.Playing)
            {
                Main.StatusWindow.Inlines.Add(new Run("None") { Foreground = Brushes.Gray });
            }
            else
            {
                string fileName = MainWindow.currentBeatmapPath;
                Console.WriteLine(fileName);
                int i;
                for (i = fileName.Length - 1; i >= 7; i--)
                {
                    if (fileName[i] == '\\')
                    {
                        break;
                    }

                }
                fileName = fileName.Substring(i + 1);
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

        public GameState UpdateGameState()
        {
            if (MainWindow.osu.getOsuProcess() == null)
            {
                state = GameState.NotOpen;
            }
            else if (MainWindow.osu.getWindowTitle().IndexOf("-", StringComparison.InvariantCulture) > -1)
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
        private GameState state = GameState.NotOpen;
    }
}
