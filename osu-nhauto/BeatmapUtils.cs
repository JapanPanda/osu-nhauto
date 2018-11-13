using System;
using osu.Shared;
using osu_database_reader.Components.Beatmaps;
using osu_nhauto.HitObjects;

namespace osu_nhauto
{
    class BeatmapUtils
    {
        public static int GetTimeDiffFromNextObj(HitObject hitObj)
        {
            int index = beatmap.GetHitObjects().IndexOf(hitObj);
            if (index >= beatmap.GetHitObjects().Count - 1)
                return int.MaxValue;

            return beatmap.GetHitObjects()[index + 1].Time - hitObj.EndTime;
        }

        public static int CalculateSliderDuration(HitObjectSlider obj) => obj.Duration;

        public static TimingPoint GetNextTimingPoint(ref int index)
        {
            if (index >= beatmap.GetTimingPoints().Count)
                return null;

            for (; index < beatmap.GetTimingPoints().Count; ++index)
            {
                TimingPoint next = beatmap.GetTimingPoints()[index];
                TimingPoint after = index + 1 >= beatmap.GetTimingPoints().Count ? null : beatmap.GetTimingPoints()[index + 1];

                if (next.MsPerQuarter > 0)
                {
                    nextTimings[0] = next.MsPerQuarter;
                    nextTimings[1] = 1;
                }
                else if (next.MsPerQuarter < 0)
                    nextTimings[1] = -100 / next.MsPerQuarter;

                if (after == null || after.Time > next.Time)
                    return next;
            }
            return null;
        }

        public static void UpdateTimingSettings()
        {
            MsPerQuarter = nextTimings[0];
            SpeedVelocity = nextTimings[1];
        }

        public static void InitializeBeatmap(CurrentBeatmap cb)
        {
            beatmap = cb;
            MsPerQuarter = beatmap.GetTimingPoints()[0].MsPerQuarter;
            CirclePxRadius = (float)(54.4 - 4.48 * beatmap.CircleSize);
            SpeedVelocity = 1;
        }

        private static CurrentBeatmap beatmap;
        private static double[] nextTimings = { 1000, 1 };
        public static double SpeedVelocity { get; private set; } = 1;
        public static double MsPerQuarter { get; private set; } = 1000;
        public static float CirclePxRadius { get; private set; }
    }
}
