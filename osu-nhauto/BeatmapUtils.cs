using System;
using osu.Shared;
using osu_database_reader.Components.Beatmaps;
using osu_nhauto.HitObjects;

namespace osu_nhauto
{
    class BeatmapUtils
    {
        public static void InitializeBeatmap(CurrentBeatmap cb)
        {
            beatmap = cb;
            MsPerQuarter = beatmap.GetTimingPoints()[0].MsPerQuarter;
            CirclePxRadius = (float)(54.4 - 4.48 * beatmap.CircleSize);
            SpeedVelocity = 1;
            TimeFadeIn = 800;
            if (cb.ApproachRate > 5)
                TimeFadeIn -= 500 * (cb.ApproachRate - 5) / 5;
            else if (cb.ApproachRate < 5)
                TimeFadeIn += 400 * (5 - cb.ApproachRate) / 5;
        }

        private static CurrentBeatmap beatmap;
        private static double[] nextTimings = { 1000, 1 };
        public static double SpeedVelocity { get; private set; } = 1;
        public static double MsPerQuarter { get; private set; } = 1000;
        public static float CirclePxRadius { get; private set; }
        public static float TimeFadeIn { get; private set; }
    }
}
