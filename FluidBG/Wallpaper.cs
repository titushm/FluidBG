using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace FluidBG; 

public static class Wallpaper
{
    private static int SPI_SETDESKWALLPAPER = 20;
    private static int SPIF_UPDATEINIFILE = 0x01;
    private static int SPIF_SENDWININICHANGE = 0x02;

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);
    public static void Set(string path, int style = 0)
    {
        RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true);
        key.SetValue(@"WallpaperStyle", style.ToString());
        key.SetValue(@"TileWallpaper", "0");
        SystemParametersInfo(SPI_SETDESKWALLPAPER,  0, path, SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);
    }
}