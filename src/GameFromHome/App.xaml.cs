using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text.Json;
using System.Windows;
using GameFromHome.Core;

namespace GameFromHome;

public partial class App : Application
{
    private Mutex? mutex;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if(SystemParameters.HighContrast)
        {
            Resources["BackgroundBrush"]=SystemColors.WindowBrush;Resources["PanelBrush"]=SystemColors.WindowBrush;
            Resources["TextBrush"]=SystemColors.WindowTextBrush;Resources["MutedBrush"]=SystemColors.WindowTextBrush;
            Resources["LineBrush"]=SystemColors.WindowTextBrush;Resources["AccentBrush"]=SystemColors.HighlightBrush;
        }
        string? Arg(string name) { int i=Array.IndexOf(e.Args,name); return i>=0&&i+1<e.Args.Length?e.Args[i+1]:null; }
        string data=Arg("--data-dir")??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"GameFromHome");
        if(e.Args.Contains("--diagnose"))
        {
            try { var discovery=new Discovery(); var apps=discovery.Scan(); File.WriteAllText(Arg("--output")??Path.Combine(Environment.CurrentDirectory,"gfh-diagnostics.json"),JsonSerializer.Serialize(new{Memory=Discovery.ReadMemory(),Apps=apps,discovery.InaccessibleCandidates,ProcessContext.LastQueryError},new JsonSerializerOptions{WriteIndented=true})); Shutdown(0); }
            catch(Exception ex) { File.WriteAllText(Arg("--output")??"gfh-diagnostics.json",ex.ToString()); Shutdown(1); }
            return;
        }
        string? capture=Arg("--capture");
        if(capture is null)
        {
            mutex=new Mutex(true,"Local\\GameFromHome-"+WindowsIdentity.GetCurrent().User?.Value,out bool first);
            if(!first) { foreach(var p in Process.GetProcessesByName("GameFromHome")) { if(p.Id!=Environment.ProcessId && p.MainWindowHandle!=0){ NativeWindow.BringForward(p.MainWindowHandle);break;}p.Dispose(); } Shutdown();return; }
        }
        DispatcherUnhandledException+=(_,args)=>{MessageBox.Show("Game From Home stopped this action.\n\n"+args.Exception.Message,"Game From Home",MessageBoxButton.OK,MessageBoxImage.Error);args.Handled=true;Shutdown(1);};
        var window=new MainWindow(new ProfileStore(data),e.Args.Contains("--run-profile"),capture);
        if(capture is not null)
        {
            // Render verification screenshots without a second on-screen window or taskbar entry.
            window.ShowActivated=false;
            window.ShowInTaskbar=false;
            window.WindowStartupLocation=WindowStartupLocation.Manual;
            window.Left=-32000;window.Top=-32000;
        }
        MainWindow=window; window.Show();
        await Task.CompletedTask;
    }
    protected override void OnExit(ExitEventArgs e){mutex?.Dispose();base.OnExit(e);}
}
internal static class NativeWindow
{
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint h);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool ShowWindow(nint h,int c);
    public static void BringForward(nint h){ShowWindow(h,9);SetForegroundWindow(h);}
}
