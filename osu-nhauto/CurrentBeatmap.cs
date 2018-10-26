using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using osu.Helpers;
using osu_database_reader.BinaryFiles;
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

        public ReadOnlyCollection<HitObject> Parse()
        {
            if (hitObjects != null)
                return hitObjects;

            if (filePath == null)
                filePath = Get();

            List<HitObject> hitObjsTemp = new List<HitObject>();
            using (var sr = new StreamReader(File.OpenRead(filePath)))
            {
                string line;
                bool startParsingObjects = false;
                while ((line = sr.ReadLine()) != null)
                {
                    if (startParsingObjects)
                    {
                        HitObject hitObj = HitObject.FromString(line);
                        hitObjsTemp.Add(hitObj);
                        Console.WriteLine("Type: {0} | X: {1}, Y: {2} | ms: {3}", hitObj.Type.ToString(), hitObj.X, hitObj.Y, hitObj.Time);
                    }
                    else if (line.Equals("[HitObjects]"))
                        startParsingObjects = true;
                }
            }

            hitObjects = hitObjsTemp.AsReadOnly();
            return hitObjects;
        }

        private InterProcessOsu ipc;
        private string filePath = null;
        private ReadOnlyCollection<HitObject> hitObjects = null;
    }
}
