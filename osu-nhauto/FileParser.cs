﻿using System;
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

        public void getBaseFilePath(Process osuProcess)
        {
            this.baseFilePath = osuProcess.MainModule.FileName;
            // Trims osu!.exe at the end
            this.baseFilePath = this.baseFilePath.Substring(0, this.baseFilePath.Length - 8);
            Console.WriteLine(baseFilePath);
        }

        public string findFilePath(string windowTitle)
        {

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
        
        public string getFileName() => this.fileName;

        private string fileName;
        private string baseFilePath;
    }
}
