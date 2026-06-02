using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace Soundboard;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        if (!IsAdministrator())
        {
            var psi = new ProcessStartInfo
            {
                FileName = Application.ExecutablePath,
                UseShellExecute = true,
                Verb = "runas",
            };
            try { Process.Start(psi); } catch { }
            return;
        }

        try { Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High; } catch { }
        try { SetProcessDPIAware(); } catch { }
        ApplicationConfiguration.Initialize();

        using (var charityForm = new CharityDonationForm())
        {
            charityForm.ShowDialog();
        }

        Application.Run(new MainForm());
    }

    private static bool IsAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }

    [DllImport("user32.dll")]
    private static extern bool SetProcessDPIAware();
}
