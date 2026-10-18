using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace PureWin.Services
{
    public static class RestorePointService
    {
        public static async Task<bool> CreateRestorePointAsync(string description = "PureWin_Sistem_Yedegi")
        {
            return await Task.Run(() =>
            {
                try
                {
                    string script = $@"
                        try {{
                            Set-ItemProperty -Path 'HKLM:\Software\Microsoft\Windows NT\CurrentVersion\SystemRestore' -Name 'SystemRestorePointCreationFrequency' -Value 0 -Type DWord -ErrorAction SilentlyContinue
                            Enable-ComputerRestore -Drive 'C:\' -ErrorAction SilentlyContinue
                            Checkpoint-Computer -Description '{description}' -RestorePointType 'MODIFY_SETTINGS' -ErrorAction Stop
                            exit 0
                        }} catch {{
                            exit 1
                        }}
                    ";

                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };

                    using var process = Process.Start(psi);
                    if (process != null)
                    {
                        process.WaitForExit(30000);
                        return process.ExitCode == 0;
                    }
                    return false;
                }
                catch
                {
                    return false;
                }
            });
        }
    }
}
