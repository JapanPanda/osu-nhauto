using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using osu.Helpers;
using osu_database_reader.BinaryFiles;
using osu_database_reader.Components.Beatmaps;
using osu_database_reader.Components.HitObjects;

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

                if (map.GameMode != osu.Shared.GameMode.Standard)
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

            List<TimingPoint> timingPtsTemp = new List<TimingPoint>();
            List<HitObject> hitObjsTemp = new List<HitObject>();
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
                        CircleSize = double.Parse(line.Split(':')[1]);
                    else if (line.StartsWith("SliderMultiplier:"))
                        SliderVelocity = double.Parse(line.Split(':')[1]);
                    else if (line.StartsWith("OverallDifficulty:"))
                        JudgementDifficulty = double.Parse(line.Split(':')[1]);
                    else if (line.Equals("[TimingPoints]"))
                        startParsing = 1;
                    else if (line.Equals("[HitObjects]"))
                        startParsing = 2;
                    else if (startParsing == 1)
                        timingPtsTemp.Add(TimingPoint.FromString(line));
                    else if (startParsing == 2)
                        hitObjsTemp.Add(HitObject.FromString(line));
                }
            }

            timingPoints = timingPtsTemp.AsReadOnly();
            hitObjects = hitObjsTemp.AsReadOnly();
        }

        public ReadOnlyCollection<TimingPoint> GetTimingPoints() => timingPoints;
        public ReadOnlyCollection<HitObject> GetHitObjects() => hitObjects;

        public double CircleSize { get; private set; }
        public double JudgementDifficulty { get; private set; }
        public double SliderVelocity { get; private set; }

        private InterProcessOsu ipc;
        private string filePath = null;
        private ReadOnlyCollection<TimingPoint> timingPoints = null;
        private ReadOnlyCollection<HitObject> hitObjects = null;
    }
}
