using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace osu_nhauto
{
    class ResolutionUtils
    {
        public static void CalculatePlayfieldResolution()
        {
            Osu.RECT wResolution = MainWindow.osu.GetWindowResolution();
            Osu.RECT cResolution = MainWindow.osu.GetClientResolution();
            Console.WriteLine("Left: {0} x Right: {1} x Top: {2} x Bottom: {3}", wResolution.Left, wResolution.Right, wResolution.Top, wResolution.Bottom);
            Console.WriteLine("{0} x {1}", wResolution.Right - wResolution.Left - 6, wResolution.Bottom - wResolution.Top - 29 - 6);
            float borderThickness = (wResolution.Right - wResolution.Left - cResolution.Right) / 2;
            float titlebarHeight = wResolution.Bottom - wResolution.Top - cResolution.Bottom - 2 * borderThickness;
            float playfieldY = 0.8f * cResolution.Bottom;
            float playfieldX = playfieldY * 4 / 3;
            Console.WriteLine("CALCULATED PLAYFIELD: {0} x {1}", playfieldX, playfieldY);

            float playfieldOffsetX = (cResolution.Right - playfieldX) / 2 + borderThickness / 2 + 1;
            float playfieldOffsetY = (cResolution.Bottom - 0.95385f * playfieldY) / 2 + titlebarHeight + borderThickness / 2; // 0.95385
            Console.WriteLine("CALCULATED OFFSETS: {0} x {1}", playfieldOffsetX, playfieldOffsetY);

            Ratio = new Vec2Float(playfieldX / 512, playfieldY / 384);
            totalOffset = new Vec2Float(wResolution.Left + playfieldOffsetX, wResolution.Top + playfieldOffsetY);
            Console.WriteLine($"CALCULATED RATIOS: {Ratio.X} x {Ratio.Y}");

            CenterPos = new Vec2Float(wResolution.Left + (wResolution.Right - wResolution.Left) / 2f, wResolution.Top + titlebarHeight + (wResolution.Bottom - wResolution.Top) / 2f);
            Console.WriteLine("CALCULATED CENTER: {0} x {1}", CenterPos.X, CenterPos.Y);
        }

        public static float ConvertToScreenXCoord(float f) => f * Ratio.X + totalOffset.X;
        public static float ConvertToScreenYCoord(float f) => f * Ratio.Y + totalOffset.Y;

        private static Vec2Float totalOffset;

        public static Vec2Float Ratio { get; private set; }
        public static Vec2Float CenterPos { get; private set; }
    }
}
