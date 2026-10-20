using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace PureWin.Services
{
    public static class SystemSettingsService
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, int[] pvParam, uint fWinIni);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref STICKYKEYS pvParam, uint fWinIni);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref bool pvParam, uint fWinIni);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref uint pvParam, uint fWinIni);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref ANIMATIONINFO pvParam, uint fWinIni);

        [StructLayout(LayoutKind.Sequential)]
        private struct STICKYKEYS
        {
            public uint cbSize;
            public uint dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ANIMATIONINFO
        {
            public uint cbSize;
            public int iMinAnimate;
        }

        private const uint SPI_GETMOUSE = 0x0003;
        private const uint SPI_SETMOUSE = 0x0004;
        private const uint SPI_GETSTICKYKEYS = 0x003A;
        private const uint SPI_SETSTICKYKEYS = 0x003B;
        private const uint SPI_GETCLIENTAREAANIMATION = 0x1042;
        private const uint SPI_SETCLIENTAREAANIMATION = 0x1043;
        private const uint SPI_GETANIMATION = 0x0048;
        private const uint SPI_SETANIMATION = 0x0049;
        private const uint SPI_GETMENUSHOWDELAY = 0x006A;
        private const uint SPI_SETMENUSHOWDELAY = 0x006B;

        private const uint SPIF_UPDATEINIFILE = 0x01;
        private const uint SPIF_SENDCHANGE = 0x02;

        private const uint SKF_HOTKEYACTIVE = 0x00000004;

        #region Fare İvmesi (Pointer Precision / Mouse Acceleration)
        /// <summary>
        /// Canlı Windows User32 oturum belleğinde ve kayıt defterinde fare ivmesinin kapalı olup olmadığını sorgular
        /// </summary>
        public static bool IsMouseAccelerationDisabled()
        {
            try
            {
                int[] m = new int[3];
                if (SystemParametersInfo(SPI_GETMOUSE, 0, m, 0))
                {
                    // m[2] == 0: ivme tamamen devre dışı (Ham 1:1 tepki)
                    return m[2] == 0;
                }

                // Fallback: Kayıt defteri kontrolü
                using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Mouse", false);
                string speed = key?.GetValue("MouseSpeed")?.ToString() ?? "1";
                return speed == "0";
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Fare ivmesini anında Windows User32 oturum belleğinde kapatır/açar ve kayıt defterine yazar
        /// </summary>
        public static void SetMouseAccelerationState(bool disable)
        {
            try
            {
                // disable = true: İvme KAPALI {0, 0, 0}
                // disable = false: İvme AÇIK (Windows Standart {6, 10, 1})
                int[] m = disable ? new int[] { 0, 0, 0 } : new int[] { 6, 10, 1 };
                SystemParametersInfo(SPI_SETMOUSE, 0, m, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);

                // Kayıt defterini kalıcı olarak güncelle
                using var key = Registry.CurrentUser.CreateSubKey(@"Control Panel\Mouse");
                if (key != null)
                {
                    key.SetValue("MouseSpeed", disable ? "0" : "1");
                    key.SetValue("MouseThreshold1", disable ? "0" : "6");
                    key.SetValue("MouseThreshold2", disable ? "0" : "10");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SetMouseAccelerationState error: {ex.Message}");
            }
        }
        #endregion

        #region Yapışkan Tuşlar (Sticky Keys Kısayolu)
        /// <summary>
        /// 5 kez Shift tuşuna basınca çıkan Yapışkan Tuşlar kısayolunun devre dışı olup olmadığını denetler
        /// </summary>
        public static bool IsStickyKeysShortcutDisabled()
        {
            try
            {
                var sk = new STICKYKEYS { cbSize = (uint)Marshal.SizeOf<STICKYKEYS>() };
                if (SystemParametersInfo(SPI_GETSTICKYKEYS, sk.cbSize, ref sk, 0))
                {
                    return (sk.dwFlags & SKF_HOTKEYACTIVE) == 0;
                }

                using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Accessibility\StickyKeys", false);
                string flags = key?.GetValue("Flags")?.ToString() ?? "510";
                if (int.TryParse(flags, out int f))
                    return (f & (int)SKF_HOTKEYACTIVE) == 0;

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Yapışkan Tuşlar kısayolunu anında Win32 API ve kayıt defteri üzerinden açar/kapatır
        /// </summary>
        public static void SetStickyKeysShortcutState(bool disable)
        {
            try
            {
                var sk = new STICKYKEYS { cbSize = (uint)Marshal.SizeOf<STICKYKEYS>() };
                if (SystemParametersInfo(SPI_GETSTICKYKEYS, sk.cbSize, ref sk, 0))
                {
                    if (disable)
                        sk.dwFlags &= ~SKF_HOTKEYACTIVE;
                    else
                        sk.dwFlags |= SKF_HOTKEYACTIVE;

                    SystemParametersInfo(SPI_SETSTICKYKEYS, sk.cbSize, ref sk, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
                }

                using var key = Registry.CurrentUser.CreateSubKey(@"Control Panel\Accessibility\StickyKeys");
                key?.SetValue("Flags", disable ? "506" : "510");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SetStickyKeysShortcutState error: {ex.Message}");
            }
        }
        #endregion

        #region Menü Açılış Gecikmesi (MenuShowDelay)
        public static bool IsMenuDelayReduced()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", false);
                string delay = key?.GetValue("MenuShowDelay")?.ToString() ?? "400";
                return delay == "0";
            }
            catch
            {
                return false;
            }
        }

        public static void SetMenuShowDelay(bool reduce)
        {
            try
            {
                uint delay = reduce ? 0u : 400u;
                SystemParametersInfo(SPI_SETMENUSHOWDELAY, delay, IntPtr.Zero, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);

                using var key = Registry.CurrentUser.CreateSubKey(@"Control Panel\Desktop");
                key?.SetValue("MenuShowDelay", reduce ? "0" : "400");
            }
            catch { }
        }
        #endregion

        #region İstemci Animasyon Efektleri (Client Area Animations)
        public static bool IsAnimationDisabled()
        {
            try
            {
                bool anim = true;
                if (SystemParametersInfo(SPI_GETCLIENTAREAANIMATION, 0, ref anim, 0))
                {
                    return !anim;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public static void SetClientAnimationState(bool disable)
        {
            try
            {
                SystemParametersInfo(SPI_SETCLIENTAREAANIMATION, 0, (IntPtr)(disable ? 0 : 1), SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
            }
            catch { }
        }

        /// <summary>
        /// "Pencereler küçültülüp büyütülürken animasyon oynat" (MinAnimate) ayarının kapalı olup olmadığını
        /// canlı User32 SPI sorgusuyla döndürür; SPI okunamazsa kayıt defterine düşer.
        /// </summary>
        public static bool IsWindowAnimationDisabled()
        {
            try
            {
                var ai = new ANIMATIONINFO { cbSize = (uint)Marshal.SizeOf<ANIMATIONINFO>() };
                if (SystemParametersInfo(SPI_GETANIMATION, ai.cbSize, ref ai, 0))
                {
                    return ai.iMinAnimate == 0;
                }
            }
            catch { }

            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop\WindowMetrics", false);
                return (key?.GetValue("MinAnimate")?.ToString() ?? "1") == "0";
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// "Pencereler küçültülüp büyütülürken animasyon oynat" ayarını (MinAnimate) anında kapatır/açar.
        /// Böylece ekran yenilemeye gerek kalmadan klasör penceresi açma/kapama animasyonu geri gelir.
        /// </summary>
        public static void SetWindowAnimationState(bool disable)
        {
            try
            {
                var ai = new ANIMATIONINFO
                {
                    cbSize = (uint)Marshal.SizeOf<ANIMATIONINFO>(),
                    iMinAnimate = disable ? 0 : 1
                };
                SystemParametersInfo(SPI_SETANIMATION, ai.cbSize, ref ai, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
            }
            catch { }
        }
        #endregion

        #region Hazırda Bekletme (Hibernation / hiberfil.sys)
        public static bool IsHibernationDisabled()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Power", false);
                if (key != null)
                {
                    var val = key.GetValue("HibernateEnabled");
                    if (val is int intVal)
                    {
                        return intVal == 0;
                    }
                }
                return !File.Exists(@"C:\hiberfil.sys");
            }
            catch
            {
                return !File.Exists(@"C:\hiberfil.sys");
            }
        }

        public static bool SetHibernationState(bool disable)
        {
            try
            {
                using var p = Process.Start(new ProcessStartInfo
                {
                    FileName = "powercfg.exe",
                    Arguments = disable ? "/h off" : "/h on",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
                p?.WaitForExit(3000);
                return true;
            }
            catch
            {
                return false;
            }
        }
        #endregion
    }
}
