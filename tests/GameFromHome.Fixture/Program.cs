using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
namespace GameFromHome.Fixture;
internal static class Program
{
    [STAThread] private static void Main(string[] args)
    {
        string mode=args.FirstOrDefault()??"normal";
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        // WPF also owns an invisible application window that participates in end-session messages.
        app.SessionEnding+=(_,e)=>{if(mode=="decline")e.Cancel=true;};
        var memory=new byte[64*1024*1024];for(int i=0;i<memory.Length;i+=4096)memory[i]=1;
        var window=new Window{Title="GFH disposable test — "+mode,Width=260,Height=100,ShowActivated=false,ShowInTaskbar=false,Content="Disposable cleanup test fixture"};
        window.SourceInitialized+=(_,_)=>HwndSource.FromHwnd(new WindowInteropHelper(window).Handle).AddHook((nint h,int msg,nint w,nint l,ref bool handled)=> {
            if(msg==0x11){handled=true;return mode=="decline"?0:1;}
            if(msg==0x16&&w!=0){handled=true;if(mode!="decline")app.Shutdown();return 0;}
            return 0;
        });
        window.Closing+=(_,e)=>{if(mode=="decline")e.Cancel=true;else app.Shutdown();};
        window.Loaded+=(_,_)=>{if(mode=="hidden")window.Hide();};
        var lifetime=new DispatcherTimer{Interval=TimeSpan.FromSeconds(75)};lifetime.Tick+=(_,_)=>app.Shutdown();lifetime.Start();
        app.Run(window);GC.KeepAlive(memory);
    }
}
