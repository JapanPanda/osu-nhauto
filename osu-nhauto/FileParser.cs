using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace osu_nhauto
{
    public class FileParser
    {
        MainWindow Main;

        public FileParser(MainWindow mw)
        {
            Main = mw;
        }

        public string GetBaseFilePath()
        {
            if (this.baseFilePath == null)
                this.baseFilePath = MainWindow.osu.GetProcess().MainModule.FileName.Substring(0, MainWindow.osu.GetProcess().MainModule.FileName.Length - 8);

            Console.WriteLine(baseFilePath);
            return this.baseFilePath;
        }

        public string FindFilePath()
        {
            if (this.baseFilePath == null)
                GetBaseFilePath();

            string windowTitle = MainWindow.osu.GetProcess().MainWindowTitle;
            if (windowTitle.Length < 8)
            {
                throw new System.Exception("Title not long enough");
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
            difficultyStrippedTitle = difficultyStrippedTitle.Replace(".", "");
            this.fileName = difficultyStrippedTitle;
            string[] songFolderList = Directory.GetDirectories(baseFilePath + "Songs\\", "*" + difficultyStrippedTitle);

            if (songFolderList.Length > 1)
            {
                this.fileName = "Duplicate Folders Found";
                return this.fileName;
            }
           
            string[] osuFileList = Directory.GetFiles(songFolderList[0], "*" + difficulty + ".osu");
            if (osuFileList.Length > 1)
            {
                this.fileName = "Duplicate .osu Files Found";
                return this.fileName;
            }
            return osuFileList[0];
        }
        
        public string GetFileName() => this.fileName;

        private string fileName;
        private string baseFilePath = null;
    }
}
