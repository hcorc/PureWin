using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace PureWin.Services
{
    public static class WallpaperService
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

        [DllImport("user32.dll")]
        private static extern bool SetSysColors(int cElements, int[] lpaElements, uint[] lpaRgbValues);

        private const int SPI_SETDESKWALLPAPER = 20;
        private const int SPIF_UPDATEINIFILE = 0x01;
        private const int SPIF_SENDCHANGE = 0x02;
        private const int COLOR_DESKTOP = 1;

        /// <summary>
        /// Windows'un şu an yerel olarak "Düz Renk - Siyah" modunda olup olmadığını denetler
        /// </summary>
        public static bool IsSolidBlack()
        {
            try
            {
                using var wpKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Wallpapers", false);
                int bgType = Convert.ToInt32(wpKey?.GetValue("BackgroundType") ?? 0);

                using var deskKey = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", false);
                string wp = deskKey?.GetValue("Wallpaper")?.ToString() ?? "";

                using var colKey = Registry.CurrentUser.OpenSubKey(@"Control Panel\Colors", false);
                string bgCol = colKey?.GetValue("Background")?.ToString() ?? "";

                return bgType == 1 && string.IsNullOrEmpty(wp) && bgCol == "0 0 0";
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Hiçbir resim dosyası kullanmadan doğrudan Windows Ayarları'ndaki yerel "Düz Renk" (Solid Color) moduna geçirir
        /// </summary>
        public static void SetSolidBlackState(bool enable)
        {
            try
            {
                if (enable)
                {
                    // 1. Kullanıcının mevcut kişisel duvar kağıdı yolunu yedekle
                    string currentWp = "";
                    using (var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", false))
                    {
                        currentWp = key?.GetValue("Wallpaper")?.ToString() ?? "";
                    }

                    if (!string.IsNullOrEmpty(currentWp))
                    {
                        using var pureWinKey = Registry.CurrentUser.CreateSubKey(@"Software\PureWin");
                        pureWinKey.SetValue("PreviousWallpaper", currentWp);
                    }

                    // 2. Windows duvar kağıdı resmini kaldır ve ekranı düz renge geçir
                    SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, "", SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);

                    // 3. Canlı sistem rengini saf siyah RGB(0,0,0) yap
                    SetSysColors(1, new int[] { COLOR_DESKTOP }, new uint[] { 0x000000 });

                    // 4. Windows Ayarları'nda (Kişiselleştirme > Arka Plan) "Düz Renk" ve "Siyah" olarak işaretle
                    using (var colorsKey = Registry.CurrentUser.CreateSubKey(@"Control Panel\Colors"))
                    {
                        colorsKey.SetValue("Background", "0 0 0");
                    }
                    using (var deskKey = Registry.CurrentUser.CreateSubKey(@"Control Panel\Desktop"))
                    {
                        deskKey.SetValue("Wallpaper", "");
                    }
                    using (var wpKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Wallpapers"))
                    {
                        wpKey.SetValue("BackgroundType", 1, RegistryValueKind.DWord);
                    }
                }
                else
                {
                    // 1. Daha önce kaydedilen kişisel duvar kağıdını bul (yoksa varsayılan Windows 11 Bloom)
                    string restoreWp = "";
                    using (var pureWinKey = Registry.CurrentUser.OpenSubKey(@"Software\PureWin", false))
                    {
                        restoreWp = pureWinKey?.GetValue("PreviousWallpaper")?.ToString() ?? "";
                    }

                    if (string.IsNullOrEmpty(restoreWp) || !File.Exists(restoreWp))
                    {
                        restoreWp = @"C:\Windows\Web\Wallpaper\Windows\img0.jpg";
                    }

                    // 2. Windows Ayarları'nda arka plan tipini tekrar "Resim" (0) moduna döndür
                    using (var wpKey = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Wallpapers"))
                    {
                        wpKey.SetValue("BackgroundType", 0, RegistryValueKind.DWord);
                    }

                    // 3. Orijinal duvar kağıdını sisteme yeniden uygula
                    if (File.Exists(restoreWp))
                    {
                        SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, restoreWp, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Wallpaper error: {ex.Message}");
            }
        }
    }
}
