using System;
using System.IO;

namespace osu_nhauto
{
    public class FileParser
    {
        public FileParser(MainWindow mw)
        {
            main = mw;
        }

        public string GetBaseFilePath()
        {
            if (this.baseFilePath == null)
                this.baseFilePath = MainWindow.osu.GetProcess().MainModule.FileName.Substring(0, MainWindow.osu.GetProcess().MainModule.FileName.Length - 8);

            return baseFilePath;
        }

        public string FindFilePath()
        {
            if (baseFilePath == null)
                GetBaseFilePath();

            string windowTitle = MainWindow.osu.GetProcess().MainWindowTitle;
            if (windowTitle.Length < 8)
            {
                throw new Exception("Title not long enough");
            }
            string strippedWindowTitle = windowTitle.Substring(8);
            string difficultyStrippedTitle = string.Empty;
            string difficulty = string.Empty;
            
            for (int i = strippedWindowTitle.Length - 1; i >= 0; i--)
            {
                if (strippedWindowTitle[i] == '[')
                {
                    difficultyStrippedTitle = strippedWindowTitle.Substring(0, i - 1);
                    difficulty = strippedWindowTitle.Substring(i);
                }
            }
            difficultyStrippedTitle = difficultyStrippedTitle.Replace(".", string.Empty);
            difficultyStrippedTitle = difficultyStrippedTitle.Replace('/', '_');
            fileName = difficultyStrippedTitle;
            string[] songFolderList = Directory.GetDirectories(baseFilePath + "Songs\\", "*" + difficultyStrippedTitle);

            if (songFolderList.Length > 1)
            {
                fileName = "Duplicate Folders Found";
                return fileName;
            }
           
            string[] osuFileList = Directory.GetFiles(songFolderList[0], "*" + difficulty + ".osu");
            if (osuFileList.Length > 1)
            {
                fileName = "Duplicate .osu Files Found";
                return fileName;
            }
            return osuFileList[0];
        }
        
        public string GetFileName() => fileName;

        private string fileName;
        private string baseFilePath = null;

        private readonly MainWindow main;
    }
}
