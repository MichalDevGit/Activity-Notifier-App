namespace ActivityNotifier.App;

/// <summary>
/// Application entry point. For now (M0) it only starts and exits.
/// Later milestones add the single-instance mutex, the Generic Host, and the tray message loop.
/// </summary>
internal static class Program
{
    /// <summary>The first method Windows runs when the .exe starts.</summary>
    // WinForms requires the UI thread to be a Single-Threaded Apartment (COM threading model).
    [STAThread]
    private static void Main()
    {
    }
}
