using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;

namespace osu_nhauto {

    public enum GameState
    {
        Error, NotOpen, Loading, Idle, Playing
    }

    public class StatusHandler
    {
        MainWindow Main;

        public StatusHandler(MainWindow mw)
        {
            Main = mw;
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
                case GameState.Loading:
                    Main.StatusWindow.Inlines.Add(new Run("Loading") { Foreground = Brushes.Green });
                    break;
                case GameState.Error:
                    Main.StatusWindow.Inlines.Add(new Run("Error Loading osu!Nhauto") { Foreground = Brushes.DarkRed });
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
                int i = fileName.Length - 1;
                while (i >= 7)
                    if (fileName[i--] == '\\')
                        break;
                fileName = fileName.Substring(0, fileName.Length - 4).Substring(i + 2);
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
            Player player = Main.GetPlayer();
            Main.StatusWindow.Inlines.Add("\n");
            Main.StatusWindow.Inlines.Add(new Run("AutoPilot: ") { FontWeight = FontWeights.Bold });
            if (player.IsAutoPilotRunning())
            {
                Main.StatusWindow.Inlines.Add(new Run("Running") { Foreground = Brushes.Green });
            }
            else
            {
                Main.StatusWindow.Inlines.Add(new Run("Not Running") { Foreground = Brushes.Red });
            }
            Main.StatusWindow.Inlines.Add("\n");
            Main.StatusWindow.Inlines.Add(new Run("Relax: ") { FontWeight = FontWeights.Bold });
            if (player.IsRelaxRunning())
            {
                Main.StatusWindow.Inlines.Add(new Run("Running") { Foreground = Brushes.Green });
            }
            else
            {
                Main.StatusWindow.Inlines.Add(new Run("Not Running") { Foreground = Brushes.Red });
            }
            Main.StatusWindow.Inlines.Add("\n");
            Main.StatusWindow.Inlines.Add(new Run("Key 1: ") { FontWeight = FontWeights.Bold });
            Main.StatusWindow.Inlines.Add(player.GetKey1().ToString());
            Main.StatusWindow.Inlines.Add(" ");
            Main.StatusWindow.Inlines.Add(new Run("Key 2: ") { FontWeight = FontWeights.Bold });
            Main.StatusWindow.Inlines.Add(player.GetKey2().ToString());

        }

        public GameState UpdateGameState()
        {
            if (!MainWindow.osu.IsOpen())
            {
                MainWindow.osu.ObtainProcess();
                state = MainWindow.osu.IsOpen() ? GameState.Loading : GameState.NotOpen;
            }
            else if (state == GameState.Loading)
            {
                MainWindow.osu.ObtainAddresses();
                state = MainWindow.osu.IsAddressesLoaded() == false ? GameState.Loading : GameState.Idle;
            }
            else if (MainWindow.osu.GetWindowTitle().IndexOf("-", StringComparison.InvariantCulture) > -1 &&
                !MainWindow.osu.GetWindowTitle().EndsWith(".osu") && MainWindow.osu.GetWindowTitle().StartsWith("osu!"))
            {
                state = GameState.Playing;
            }
            else if (!MainWindow.osu.IsAddressesLoaded())
            {
                state = GameState.Error;
            }
            else
            {
                state = GameState.Idle;
            }
            return state;
        }

        public GameState GetGameState() => state;
        private GameState state = GameState.NotOpen;
    }
}
