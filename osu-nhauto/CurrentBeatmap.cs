using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using osu.Helpers;
using osu.Shared;
using osu_database_reader;
using osu_database_reader.BinaryFiles;
using osu_database_reader.Components.Beatmaps;
using nhauto = osu_nhauto.HitObjects;
using holly = osu_database_reader.Components.HitObjects;

namespace osu_nhauto {

    public class CurrentBeatmap
    {
        public CurrentBeatmap()
        {
            ipc = (InterProcessOsu)Activator.GetObject(typeof(InterProcessOsu), "ipc://osu!/loader");
        }

        public string Get()
        {
            if (filePath != null)
                return filePath;

            if (MainWindow.statusHandler.GetGameState() == GameState.NotOpen)
                return null;

            OsuDb db = OsuDb.Read(Path.Combine(MainWindow.fileParser.GetBaseFilePath(), "osu!.db"));
            string hash = new string('0', 32);

            try
            {
                var data = ipc.GetBulkClientData();
                if (data.BeatmapChecksum == hash)
                    return null;

                hash = data.BeatmapChecksum;
                var map = db.Beatmaps.Find(a => a.BeatmapChecksum == hash);
                if (map == null)
                    throw new Exception("Map not found. Using legacy finder.");

                if (map.GameMode != GameMode.Standard)
                    return null;

                filePath = Path.Combine(MainWindow.fileParser.GetBaseFilePath(), "Songs", map.FolderName, map.BeatmapFileName);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                filePath = MainWindow.fileParser.FindFilePath();
            }

            Console.WriteLine(filePath);
            return filePath;
        }

        public void Parse()
        {
            if (timingPoints != null && hitObjects != null && stackHeights != null)
                return;

            if (filePath == null)
                filePath = Get();

            Console.WriteLine("Attempting to parse beatmap");
            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();

            List<TimingPoint> timingPtsTemp = new List<TimingPoint>();
            List<nhauto.HitObject> hitObjsTemp = new List<nhauto.HitObject>();
            using (var sr = new StreamReader(File.OpenRead(filePath)))
            {
                string line;
                int startParsing = 0;
                while ((line = sr.ReadLine()) != null)
                {
                    if (line.Length == 0)
                        continue;

                    if (line.StartsWith("["))
                        startParsing = 0;

                    if (line.StartsWith("CircleSize:"))
                        CircleSize = float.Parse(line.Split(':')[1]);
                    else if (line.StartsWith("ApproachRate:"))
                        ApproachRate = float.Parse(line.Split(':')[1]);
                    else if (line.StartsWith("StackLeniency:"))
                        StackLeniency = float.Parse(line.Split(':')[1]);
                    else if (line.StartsWith("SliderMultiplier:"))
                        SliderVelocity = float.Parse(line.Split(':')[1]);
                    else if (line.StartsWith("OverallDifficulty:"))
                        JudgementDifficulty = float.Parse(line.Split(':')[1]);
                    else if (line.Equals("[TimingPoints]"))
                        startParsing = 1;
                    else if (line.Equals("[HitObjects]"))
                        startParsing = 2;
                    else if (startParsing == 1)
                        timingPtsTemp.Add(TimingPoint.FromString(line));
                    else if (startParsing == 2)
                    {
                        holly.HitObject hollyObj = holly.HitObject.FromString(line);
                        switch (hollyObj.Type & (HitObjectType)0b1000_1011)
                        {
                            case HitObjectType.Slider:
                            {
                                holly.HitObjectSlider hollySlider = hollyObj as holly.HitObjectSlider;
                                switch (hollySlider.CurveType)
                                {
                                    case CurveType.Linear:
                                        hitObjsTemp.Add(new nhauto.HitObjectSliderLinear(hollySlider, SliderVelocity, timingPtsTemp));
                                        break;
                                    case CurveType.Perfect:
                                        hitObjsTemp.Add(new nhauto.HitObjectSliderPerfect(hollySlider, SliderVelocity, timingPtsTemp));
                                        break;
                                    case CurveType.Catmull:
                                    case CurveType.Bezier:
                                        hitObjsTemp.Add(new nhauto.HitObjectSliderBezier(hollySlider, SliderVelocity, timingPtsTemp));
                                        break;
                                    default:
                                        break;
                                }
                                break;
                            }
                            case HitObjectType.Normal:
                                hitObjsTemp.Add(new nhauto.HitObjectCircle(hollyObj as holly.HitObjectCircle));
                                break;
                            case HitObjectType.Spinner:
                                hitObjsTemp.Add(new nhauto.HitObjectSpinner(hollyObj as holly.HitObjectSpinner));
                                break;
                            default:
                                break;
                        }
                    }
                }
            }

            timingPoints = timingPtsTemp.AsReadOnly();
            hitObjects = hitObjsTemp.AsReadOnly();

            //ApplyStacking(); // TODO check file format version < 6

            stopwatch.Stop();
            Console.WriteLine($"Elapsed time to parse beatmap: {stopwatch.ElapsedMilliseconds}ms");
        }

        private void ApplyStacking()
        {
            List<int> stackHeightsTemp = new List<int>(hitObjects.Count);
            for (int i = 0; i < stackHeightsTemp.Capacity; ++i)
                stackHeightsTemp.Add(0);

            float timePreempt = 1200;
            if (ApproachRate > 5)
                timePreempt -= 750 * (ApproachRate - 5) / 5;
            else if (ApproachRate < 5)
                timePreempt += 600 * (5 - ApproachRate) / 5;
            float stackThreshold = timePreempt * StackLeniency;

            int finalIndex = hitObjects.Count - 1;
            int extendedEndIndex = finalIndex;
            for (int i = finalIndex; i >= 0; --i)
            {
                int stackBaseInd = i;
                for (int j = stackBaseInd + 1; j < hitObjects.Count; ++j)
                {
                    nhauto.HitObject stackBaseObj = hitObjects[stackBaseInd];
                    if ((stackBaseObj.Type & (HitObjectType)0b1000_1011) == HitObjectType.Spinner)
                        break;

                    nhauto.HitObject currHitObj = hitObjects[j];
                    if ((currHitObj.Type & (HitObjectType)0b1000_1011) == HitObjectType.Spinner)
                        continue;

                    int stackBaseEndTime = stackBaseObj.Time; // TODO remove assumption of circle
                    if (currHitObj.Time - stackBaseEndTime > stackThreshold)
                        break;

                    if (Math.Sqrt(Math.Pow(stackBaseObj.X - currHitObj.X, 2) + Math.Pow(stackBaseObj.Y - currHitObj.Y, 2)) < 3 ||
                        (stackBaseObj.Type & (HitObjectType)0b1000_1011) == HitObjectType.Slider) // TODO calculate end position
                    {
                        stackBaseInd = j;
                        stackHeightsTemp[j] = 0;
                    }
                }

                if (stackBaseInd > extendedEndIndex)
                {
                    extendedEndIndex = stackBaseInd;
                    if (extendedEndIndex == finalIndex)
                        break;
                }
            }
        }

        public ReadOnlyCollection<TimingPoint> GetTimingPoints() => timingPoints;
        public ReadOnlyCollection<nhauto.HitObject> GetHitObjects() => hitObjects;
        public ReadOnlyCollection<int> GetStackHeights() => stackHeights;

        public float CircleSize { get; private set; }
        public float ApproachRate { get; private set; }
        public float JudgementDifficulty { get; private set; }
        public float StackLeniency { get; private set; }
        public float SliderVelocity { get; private set; }

        private InterProcessOsu ipc;
        private string filePath = null;
        private ReadOnlyCollection<TimingPoint> timingPoints = null;
        private ReadOnlyCollection<nhauto.HitObject> hitObjects = null;
        private ReadOnlyCollection<int> stackHeights = null;
    }
}
