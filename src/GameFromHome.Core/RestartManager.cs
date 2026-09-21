using System.Runtime.InteropServices;
using System.Text;

namespace GameFromHome.Core;

internal static class RestartManager
{
    [StructLayout(LayoutKind.Sequential)] internal struct UniqueProcess { public uint Pid; public Native.FileTime Started; }
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] internal struct ProcessInfo
    {
        public UniqueProcess Process;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=256)] public string AppName;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=64)] public string ServiceName;
        public int Type; public uint Status, Session;
        [MarshalAs(UnmanagedType.Bool)] public bool Restartable;
    }
    [DllImport("rstrtmgr.dll", CharSet=CharSet.Unicode)] private static extern uint RmStartSession(out uint session, uint flags, StringBuilder key);
    [DllImport("rstrtmgr.dll", CharSet=CharSet.Unicode)] private static extern uint RmRegisterResources(uint session, uint files, string[]? fileNames, uint count, UniqueProcess[] apps, uint services, string[]? serviceNames);
    [DllImport("rstrtmgr.dll")] private static extern uint RmGetList(uint session, out uint needed, ref uint count, [In,Out] ProcessInfo[]? apps, ref uint reboot);
    [DllImport("rstrtmgr.dll")] private static extern uint RmShutdown(uint session, uint flags, nint callback);
    [DllImport("rstrtmgr.dll")] private static extern uint RmEndSession(uint session);
    [DllImport("rstrtmgr.dll")] private static extern uint RmCancelCurrentTask(uint session);

    internal static (int Code, string Detail) Request(IReadOnlyList<ProcessIdentity> targets, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var code = RmStartSession(out uint session, 0, new StringBuilder(33));
        if (code != 0) return ((int)code, "Windows could not create a clean-exit session.");
        try
        {
            var unique = targets.Select(p => new UniqueProcess { Pid=(uint)p.Pid, Started=Native.FileTime.From(p.Created) }).ToArray();
            // Register only exact process instances. Never register files or services.
            code = RmRegisterResources(session, 0, null, (uint)unique.Length, unique, 0, null);
            if (code != 0) return ((int)code, "Windows could not register the selected app.");
            uint count=0, reboot=0; code=RmGetList(session, out uint needed, ref count, null, ref reboot);
            if (code is not (0 or 234)) return ((int)code, "Windows could not verify the shutdown scope.");
            ProcessInfo[] affected=[];
            for (int attempt=0; needed>0 && attempt<4; attempt++) { affected=new ProcessInfo[needed]; count=needed; code=RmGetList(session,out needed,ref count,affected,ref reboot); if(code!=234)break; }
            if(code!=0) return ((int)code,"Shutdown scope changed; no request sent.");
            if (reboot != 0) return (350, "Windows cannot cleanly close this app in the current session. No restart requested.");
            var actual=affected.Take((int)count).ToArray();
            if(actual.Any(p => !string.IsNullOrEmpty(p.ServiceName) || p.Type is 3 or 4 or 5 or 1000 || !unique.Any(t=>t.Pid==p.Process.Pid && t.Started.Value==p.Process.Started.Value))) return (5,"Windows reported a process outside the permitted app scope. Skipped.");
            if(actual.Length==0) return (0,"No registered app processes remain.");
            token.ThrowIfCancellationRequested();
            using var registration=token.Register(()=>RmCancelCurrentTask(session));
            // flags=0 explicitly excludes RmForceShutdown. An application may veto shutdown.
            code=RmShutdown(session,0,0);
            return ((int)code,code==0?"Cooperative Windows exit request completed.":code==351?"The app declined or did not finish its exit.":code==1223?"Stopped issuing exit requests.":$"Windows returned exit status {code}.");
        }
        finally { RmEndSession(session); }
    }
}
