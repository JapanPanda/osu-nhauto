using System;

namespace osu_nhauto {

    public class Player
    {
	    public Player(Osu osu)
	    {
            osuClient = osu;
        }

        private Osu osuClient;

        public void ToggleAutoPilot() => this.autopilotRunning = !this.autopilotRunning;
        public void ToggleRelax() => this.relaxRunning = !this.relaxRunning;
        public char GetKey1() => this.key1;
        public char GetKey2() => this.key2;
        public void SetKey1(char key) => this.key1 = key;
        public void SetKey2(char key) => this.key2 = key;
        public bool IsAutoPilotRunning() => this.autopilotRunning;
        public bool IsRelaxRunning() => this.relaxRunning;

        private char key1 = 'Z';
        private char key2 = 'X';
        private bool autopilotRunning = false;
        private bool relaxRunning = false;
    }
}

