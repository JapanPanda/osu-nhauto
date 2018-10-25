﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
                key1 = 'Z';
                key2 = 'X';
            }

            public void updateWindow()
            {
                Main.StatusWindow.Text = string.Empty;
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
            


            public void toggleAutoPilot() { this.autopilotRunning = !this.autopilotRunning; }
            public void toggleRelax() { this.relaxRunning = !this.relaxRunning; }
            public void setKey1(char key) { this.key1 = key; }
            public void setKey2(char key) { this.key2 = key; }
            public bool isAutoPilotRunning() { return this.autopilotRunning; }
            public bool isRelaxRunning() { return this.relaxRunning; }

            private char key1;
            private char key2;
            private bool autopilotRunning;
            private bool relaxRunning;
        }


        public void main()
        {
            // status is name of status window text block
            StatusHandler status = new StatusHandler();

            status.updateWindow();
            RelaxButton.Click += RelaxButton_Click;
            AutoPilotButton.Click += AutoPilotButton_Click;
            ApplyChanges.Click += ApplyChanges_Click;

            void RelaxButton_Click(object sender, RoutedEventArgs e)
            {
                if (status.isRelaxRunning()) {
                    RelaxButton.Content = "Enable Relax";
                }
                else
                {
                    RelaxButton.Content = "Disable Relax";
                }
                status.toggleRelax();
                status.updateWindow();
            }

            void AutoPilotButton_Click(object sender, RoutedEventArgs e)
            {
                if (status.isAutoPilotRunning())
                {
                    AutoPilotButton.Content = "Enable AutoPilot";
                }
                else
                {
                    AutoPilotButton.Content = "Disable AutoPilot";
                }
                status.toggleAutoPilot();
                status.updateWindow();
            }

            void ApplyChanges_Click(object sender, RoutedEventArgs e)
            {
                if (!Regex.IsMatch(Main.Key1TextBox.Text, @"^[a-zA-Z]+$"))
                {
                    MessageBox.Show("Invalid Input for Key 1: " + Main.Key1TextBox.Text[0] + "\nA-Z only please",
                                         "Ok",
                                         MessageBoxButton.OK,
                                         MessageBoxImage.Error);
                }
                else if (!Regex.IsMatch(Main.Key2TextBox.Text, @"^[a-zA-Z]+$"))
                {
                    MessageBox.Show("Invalid Input for Key 2: " + Main.Key2TextBox.Text[0] + "\nA-Z only please",
                                         "Ok",
                                         MessageBoxButton.OK,
                                         MessageBoxImage.Error);
                }
                else
                {
                    status.setKey1(Main.Key1TextBox.Text[0]);
                    status.setKey2(Main.Key2TextBox.Text[0]);
                    status.updateWindow();
                }
            }
        }

        public MainWindow()
        {
            Main = this;
            InitializeComponent();

            main();
        }

        private void InputBox_GotFocus(object sender, RoutedEventArgs e)
        {
            TextBox text = (TextBox)sender;
            text.Text = string.Empty;
        }

    }
}