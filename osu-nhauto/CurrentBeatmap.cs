using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using osu.Helpers;
using osu.Shared;
using osu_database_reader;
using osu_database_reader.BinaryFiles;
using osu_database_reader.Components;
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
            if (timingPoints != null && hitObjects != null)
                return;

            if (filePath == null)
                filePath = Get();

            Console.WriteLine("Attempting to parse beatmap");
            System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
            stopwatch.Start();

            ModValue = MainWindow.osu.GetModValue();
            if (!ModValue.HasValue)
                Console.WriteLine("WARNING: Mod value not found. Assuming NoMod.");

            bool modifiedSettings = false;
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
                    else if (line.StartsWith("SliderTickRate"))
                        SliderTickRate = int.Parse(line.Split(':')[1]);
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
                        if (!modifiedSettings)
                        {
                            CirclePxRadius = (float)(54.4 - 4.48 * CircleSize);
                            ModifySettingsByModValue();
                            modifiedSettings = true;
                        }
                        holly.HitObject hollyObj = holly.HitObject.FromString(line);
                        switch (hollyObj.Type & (HitObjectType)0b1000_1011)
                        {
                            case HitObjectType.Slider:
                            {
                                holly.HitObjectSlider hollySlider = hollyObj as holly.HitObjectSlider;
                                switch (hollySlider.CurveType)
                                {
                                    case CurveType.Linear:
                                        if (hollySlider.Points.Count == 1)
                                            hitObjsTemp.Add(new nhauto.HitObjectSliderLinear(hollySlider, SliderVelocity, timingPtsTemp, shouldVInvert));
                                        else
                                        {
                                            List<Vector2> duplPoints = new List<Vector2>();
                                            foreach (Vector2 v in hollySlider.Points)
                                            {
                                                duplPoints.Add(v);
                                                duplPoints.Add(v);
                                            }
                                            hollySlider.Points = duplPoints;
                                            hitObjsTemp.Add(new nhauto.HitObjectSliderBezier(hollySlider, SliderVelocity, timingPtsTemp, shouldVInvert));
                                        }
                                        break;
                                    case CurveType.Perfect:
                                        try
                                        {
                                            if (hollySlider.Points.Count == 2)
                                                hitObjsTemp.Add(new nhauto.HitObjectSliderPerfect(hollySlider, SliderVelocity, timingPtsTemp, shouldVInvert));
                                            else
                                                hitObjsTemp.Add(new nhauto.HitObjectSliderBezier(hollySlider, SliderVelocity, timingPtsTemp, shouldVInvert));
                                        }
                                        catch (Exception)
                                        {
                                            hitObjsTemp.Add(new nhauto.HitObjectSliderLinear(hollySlider, SliderVelocity, timingPtsTemp, shouldVInvert));
                                        }
                                        break;
                                    case CurveType.Catmull:
                                        hitObjsTemp.Add(new nhauto.HitObjectSliderCatmull(hollySlider, SliderVelocity, timingPtsTemp, shouldVInvert));
                                        break;
                                    case CurveType.Bezier:
                                        hitObjsTemp.Add(new nhauto.HitObjectSliderBezier(hollySlider, SliderVelocity, timingPtsTemp, shouldVInvert));
                                        break;
                                    default:
                                        break;
                                }
                                break;
                            }
                            case HitObjectType.Normal:
                                hitObjsTemp.Add(new nhauto.HitObjectCircle(hollyObj as holly.HitObjectCircle, shouldVInvert));
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

            ApplyStacking(); // TODO check file format version < 6           

            ApplyHumanStuff();

            stopwatch.Stop();
            Console.WriteLine($"Elapsed time to parse beatmap: {stopwatch.ElapsedMilliseconds}ms");
        }

        private void ApplyHumanStuff()
        {
            int offsetBoundary = Math.Max(4, (int)(CirclePxRadius / 2.5f));
            Random rand = new Random();
            Vector2? lastUnmodifiedPos = null;
            Vector2 randOffset = new Vector2(rand.Next(-offsetBoundary, offsetBoundary), rand.Next(-offsetBoundary, offsetBoundary));
            for (int i = 0; i < hitObjects.Count; ++i)
            {
                if (hitObjects[i].Type == HitObjectType.Spinner)
                    continue;

                if (!lastUnmodifiedPos.HasValue)
                    lastUnmodifiedPos = new Vector2(hitObjects[i].X, hitObjects[i].Y);
                else if (i > 0 && hitObjects[i].Time - hitObjects[i - 1].Time > TimeDiffThreshold)
                    randOffset = new Vector2(rand.Next(-offsetBoundary, offsetBoundary), rand.Next(-offsetBoundary, offsetBoundary));
                else
                {
                    hitObjects[i].Streamable = true;
                    randOffset.X += rand.Next(-1 - randOffset.X, 1 - randOffset.X);
                    randOffset.Y += rand.Next(-1 - randOffset.Y, 1 - randOffset.Y);
                }

                lastUnmodifiedPos = new Vector2(hitObjects[i].X, hitObjects[i].Y);
                hitObjects[i].X += randOffset.X;
                hitObjects[i].Y += randOffset.Y;

                if (hitObjects[i].Type == HitObjectType.Slider)
                {
                    nhauto.HitObjectSlider slider = hitObjects[i] as nhauto.HitObjectSlider;
                    int projOffset = (int)(16f * SpeedModifier);
                    Vec2Float projectedEnd = slider.GetPosition(slider.EndTime - projOffset);
                    Vec2Float actualEnd = slider.GetPosition(slider.EndTime);
                    if (i < hitObjects.Count - 1)
                    {
                        Vec2Float velProjActual = new Vec2Float(projectedEnd.X - actualEnd.X, actualEnd.Y - projectedEnd.Y);
                        Vec2Float velActualNext = new Vec2Float(hitObjects[i + 1].X - actualEnd.X, actualEnd.Y - hitObjects[i + 1].Y);
                        double angle = Math.Acos((velProjActual.X * velActualNext.X + velProjActual.Y * velActualNext.Y) / (velProjActual.Length() * velActualNext.Length()));
                        if (angle >= 2.79)
                        {
                            slider.timeStop = Math.Max(slider.Time, slider.EndTime - 5);
                            slider.timeStopFactor = 0.95f;
                        }
                        else
                            slider.timeStopFactor = Math.Min(slider.timeStopFactor, 0.95f * (float)angle / 2.79f); // 2.3562
                    }

                    float distToBall = slider.GetRelativePosition(slider.Time + (int)slider.PathTime - projOffset).Distance(slider.GetRelativePosition(slider.Time));

                    if (slider.PixelLength <= CirclePxRadius || slider.Duration <= projOffset)
                        slider.TreatAsCircle = 2;
                    else if (distToBall <= SliderBallPxRadius / 1.75f)
                        slider.TreatAsCircle = 1;

                    if (slider is nhauto.HitObjectSliderPerfect && (slider as nhauto.HitObjectSliderPerfect).circleRadius < SliderBallPxRadius / 3)
                        slider.TreatAsCircle = 2;

                    if (slider is nhauto.HitObjectSliderBezier && !(slider as nhauto.HitObjectSliderBezier).TreatAsLinear && (slider as nhauto.HitObjectSliderBezier).maxDistFromHead < SliderBallPxRadius)
                        slider.TreatAsCircle = 1;

                    if (slider.TreatAsCircle == 0 && !slider.timeStop.HasValue)
                    {
                        int stop = slider.EndTime - projOffset;
                        while (stop > slider.Time && projectedEnd.Distance(slider.GetPosition(stop)) <= SliderBallPxRadius / 1.75f)
                            stop -= 4;
                        slider.timeStop = Math.Max(slider.Time, stop);
                    }

                    if (slider.TreatAsCircle > 0 && slider.Duration <= TimeDiffThreshold / 2)
                    {
                        slider.timeStop = slider.Time;
                        slider.timeStopFactor = 0;
                    }
                }
            }
        }

        private void ModifySettingsByModValue()
        {
            if (!ModValue.HasValue)
                return;

            if ((ModValue.Value & (int)Mods.HardRock) > 0)
            {
                Console.WriteLine("Detected HardRock");
                CircleSize = Math.Min(10, CircleSize * 1.3f);
                ApproachRate = Math.Min(10, ApproachRate * 1.4f);
                JudgementDifficulty = Math.Min(10, JudgementDifficulty * 1.4f);
                shouldVInvert = true;
            }
            else if ((ModValue.Value & (int)Mods.Easy) > 0)
            {
                Console.WriteLine("Detected Easy");
                CircleSize *= 0.5f;
                ApproachRate *= 0.5f;
                JudgementDifficulty *= 0.5f;
            }

            if ((ModValue.Value & (int)(Mods.DoubleTime | Mods.Nightcore)) > 0)
            {
                Console.WriteLine("Detected DoubleTime");
                SpeedModifier = 1.5f;
            }
            else if ((ModValue.Value & (int)Mods.HalfTime) > 0)
            {
                Console.WriteLine("Detected HalfTime");
                SpeedModifier = 0.75f;
            }
            else
                SpeedModifier = 1;
        }

        private void ApplyStacking()
        {
            List<int> stackHeightsTemp = new List<int>(hitObjects.Count);
            for (int i = 0; i < stackHeightsTemp.Capacity; ++i)
                stackHeightsTemp.Add(0);

            float stackThreshold = TimePreempt * StackLeniency;
            for (int i = hitObjects.Count - 1; i > 0; --i)
            {
                nhauto.HitObject objectI = hitObjects[i];
                if (objectI.StackHeight != 0 || objectI.Type == HitObjectType.Spinner)
                    continue;

                Vec2Float objectIPosVec = new Vec2Float(objectI.X, objectI.Y);
                for (int n = i; --n >= 0;)
                {
                    nhauto.HitObject objectN = hitObjects[n];
                    if (objectN.Type == HitObjectType.Spinner)
                        continue;

                    if (objectI.Time - objectN.EndTime > stackThreshold)
                        break;

                    if (objectI.Type == HitObjectType.Normal)
                    {
                        if (objectN.Type == HitObjectType.Slider)
                        {
                            nhauto.HitObjectSlider sliderN = objectN as nhauto.HitObjectSlider;
                            Vec2Float endPosition = sliderN.GetPosition(sliderN.EndTime);
                            if (endPosition.Distance(objectIPosVec) < STACK_LENIENCE)
                            {
                                int offset = objectI.StackHeight - objectN.StackHeight + 1;
                                for (int j = n + 1; j <= i; j++)
                                {
                                    if (endPosition.Distance(hitObjects[j].X, hitObjects[j].Y) < STACK_LENIENCE)
                                        hitObjects[j].StackHeight -= offset;
                                }
                                break;
                            }
                        }

                        if (objectIPosVec.Distance(objectN.X, objectN.Y) < STACK_LENIENCE)
                        {
                            objectN.StackHeight = objectI.StackHeight + 1;
                            objectI = objectN;
                        }
                    }
                    else
                    {
                        Vec2Float endPosition = objectN.Type == HitObjectType.Slider ? (objectN as nhauto.HitObjectSlider).GetPosition(objectN.EndTime) : new Vec2Float(objectN.X, objectN.Y);
                        if (endPosition.Distance(objectIPosVec) < STACK_LENIENCE)
                        {
                            objectN.StackHeight = objectI.StackHeight + 1;
                            objectI = objectN;
                        }
                    }
                }
            }

            float stackOffset = (54.4f - 4.48f * CircleSize) / 10f;
            for (int i = 0; i < hitObjects.Count; ++i)
            {
                nhauto.HitObject hitObj = hitObjects[i];
                hitObj.X -= (int)(stackOffset * hitObj.StackHeight);
                hitObj.Y -= (int)(stackOffset * hitObj.StackHeight);
            }
        }

        public ReadOnlyCollection<TimingPoint> GetTimingPoints() => timingPoints;
        public ReadOnlyCollection<nhauto.HitObject> GetHitObjects() => hitObjects;

        public float CircleSize { get; private set; }
        public float ApproachRate { get; private set; }
        public float JudgementDifficulty { get; private set; }
        public float StackLeniency { get; private set; }
        public float SliderVelocity { get; private set; }
        public int SliderTickRate { get; private set; }

        public float CirclePxRadius { get; private set; }
        public float SliderBallPxRadius { get => CirclePxRadius * 2.3f; }
        public float SpeedModifier { get; private set; } = 1;
        public float TimePreempt { get => ApproachRate == 5 ? 1200 : 1200 + (ApproachRate > 5 ? -750 : 600) * (ApproachRate - 5) / 5; }
        public float TimeFadeIn { get => TimePreempt * 2 / 3; }
        public int TimeDiffThreshold { get => (int)(116 * SpeedModifier); }
        public int? ModValue { get; private set; }

        private InterProcessOsu ipc;
        private string filePath = null;
        private ReadOnlyCollection<TimingPoint> timingPoints = null;
        private ReadOnlyCollection<nhauto.HitObject> hitObjects = null;

        private bool shouldVInvert = false;

        private const int STACK_LENIENCE = 3;
    }
}
