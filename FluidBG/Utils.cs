using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace FluidBG;

public static class Constants {
    public static readonly Version Version = new Version(1, 0, 9);
    public static readonly string GithubRepoUrl = "https://github.com/titushm/FluidBG";
    public static readonly int[] ComboBoxSecondIntervals = { 1, 60, 3600, 86400, 604800 };
    public static RegistryKey StartupRegisteryKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
    
    public struct Paths {
        public static readonly string DataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\titushm\\FluidBG";
        public static readonly string LogFile = $"{DataFolder}\\log.tmp";
        public static readonly string ConfigFile = $"{DataFolder}\\config.json";
    }

    public static Dictionary<int, int> WallpaperModes = new(){
        {0, 10},
        {1, 6},
        {2, 2},
        {3, 0},
        {4, 0},
        {5, 22}
    };
}