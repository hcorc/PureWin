using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace PureWin.Services
{
    public static class StartMenuService
    {
        private const string PackageRelativePath = @"AppData\Local\Packages\Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy\LocalState";

        public static async Task<bool> ClearStartMenuAsync(bool allUsers = false)
        {
            return await Task.Run(() =>
            {
                try
                {
                    string templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Start", "start2.bin");
                    if (!File.Exists(templatePath))
                    {
                        // Alternatif geliştirme konumu
                        templatePath = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Start", "start2.bin");
                    }

                    if (!allUsers)
                    {
                        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                        string userDir = Path.Combine(localAppData, @"Packages\Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy\LocalState");
                        ApplyTemplate(userDir, templatePath);
                    }
                    else
                    {
                        // Tüm kullanıcı profillerini tara
                        string usersRoot = Path.GetDirectoryName(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)) ?? @"C:\Users";
                        if (Directory.Exists(usersRoot))
                        {
                            foreach (var userFolder in Directory.GetDirectories(usersRoot))
                            {
                                string targetDir = Path.Combine(userFolder, PackageRelativePath);
                                ApplyTemplate(targetDir, templatePath);
                            }
                        }

                        // Default profil
                        string defaultDir = Path.Combine(usersRoot, "Default", PackageRelativePath);
                        ApplyTemplate(defaultDir, templatePath);
                    }

                    // StartMenuExperienceHost sürecini yeniden başlat
                    try
                    {
                        foreach (var proc in Process.GetProcessesByName("StartMenuExperienceHost"))
                        {
                            proc.Kill();
                        }
                    }
                    catch { }

                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"ClearStartMenuAsync error: {ex.Message}");
                    return false;
                }
            });
        }

        private static void ApplyTemplate(string targetDir, string templatePath)
        {
            try
            {
                if (!Directory.Exists(targetDir))
                    Directory.CreateDirectory(targetDir);

                string binFile = Path.Combine(targetDir, "start2.bin");

                if (File.Exists(templatePath))
                {
                    File.Copy(templatePath, binFile, true);
                }
                else
                {
                    // Şablon dosyası bulunamazsa mevcut dosyayı silerek sıfırla
                    if (File.Exists(binFile))
                        File.Delete(binFile);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to update Start layout at {targetDir}: {ex.Message}");
            }
        }
    }
}
