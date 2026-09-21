using System.Diagnostics;
using System.Text.Json;
using GameFromHome.Core;

var results=new List<object>();int failed=0;
void Check(string name,bool condition,object? detail=null){results.Add(new{name,passed=condition,detail});Console.WriteLine($"{(condition?"PASS":"FAIL")} {name}");if(!condition)failed++;}
Check("Codex process is not ChatGPT",Catalog.Identify("ChatGPT.exe",@"C:\Program Files\WindowsApps\OpenAI.Codex_1_x64__id\app\ChatGPT.exe")?.Id=="codex");
Check("Random name collision is excluded",Catalog.Identify("msedge.exe",@"C:\unrelated\msedge.exe") is null);
Check("Discord protection is path based",Catalog.ProtectedPath(@"C:\Users\someone\AppData\Local\Discord\app-1\Discord.exe"));
Check("Negative RAM delta is honest",Formatting.Delta(-2L*1024*1024*1024).StartsWith("−2.0"));
var memory=Discovery.ReadMemory();Check("Available RAM is within usable physical memory",memory.Available>0&&memory.Available<=memory.Total);
var probe=new ProcessIdentity(42,100,@"C:\test\test.exe",0,1,"test.exe",1);
Check("PID reuse is not the same instance",!Discovery.SameInstance(probe,probe with{Created=101}));
Check("Path change is not the same instance",!Discovery.SameInstance(probe,probe with{Path=@"C:\other\test.exe"}));
var profilePath=Path.Combine(AppContext.BaseDirectory,"profile-test-"+Guid.NewGuid().ToString("N"));var store=new ProfileStore(profilePath);
store.Save(new Preferences{Configured=true,SelectedApps=["edge","discord","codex","unknown"]});
Check("Discord and unknown IDs stripped; Codex selection retained",store.Load().SelectedApps.SequenceEqual(["edge","codex"]));
File.WriteAllText(Path.Combine(profilePath,"preferences.json"),"broken");Check("Corrupt profile fails into review",!store.Load().Configured&&store.LoadWarning is not null);
Check("Codex is selectable but opt-in",Catalog.Apps.Single(a=>a.Id=="codex") is {Protected:false,DefaultSelected:false});
Check("Codex package is not hard-protected",!Catalog.ProtectedPath(@"C:\Program Files\WindowsApps\OpenAI.Codex_1_x64__id\app\ChatGPT.exe"));
Check("Bun and Node are recognized as runtimes",ProcessContext.IsRuntime("BUN.EXE")&&ProcessContext.IsRuntime("node.exe"));
Check("claude-mem worker command is recognized",ProcessContext.IsClaudeMemWorker("bun.exe",@"bun C:\plugins\claude-mem\13.25.2\scripts\worker-service.cjs --daemon"));
Check("Unrelated Bun project is not claude-mem",!ProcessContext.IsClaudeMemWorker("bun.exe",@"bun C:\projects\server.ts"));
Check("Unrelated runtime cannot spoof worker by name",!ProcessContext.IsClaudeMemWorker("python.exe",@"C:\claude-mem\scripts\worker-service.cjs"));
Check("Worker health must match exact PID",ClaudeMem.HealthMatches("{\"pid\":123,\"status\":\"ok\",\"version\":\"1\"}",123)&&!ClaudeMem.HealthMatches("{\"pid\":124,\"status\":\"ok\",\"version\":\"1\"}",123));
Check("Malformed health is rejected",!ClaudeMem.HealthMatches("invalid",123)&&!ClaudeMem.HealthMatches("{\"pid\":123}",123));
Check("Discovered app identity is path scoped",ProcessContext.AppId(@"C:\Apps\tool.exe")!=ProcessContext.AppId(@"C:\Other\tool.exe"));
var dynamicId=ProcessContext.AppId(@"C:\Apps\tool.exe");
store.Save(new Preferences{Configured=true,SelectedApps=[dynamicId],ApprovedFingerprints=new(){{dynamicId,"reviewed-installation"}}});
Check("Approved discovered app survives profile reload",store.Load().SelectedApps.SequenceEqual([dynamicId]));
store.Save(new Preferences{Configured=true,SelectedApps=[dynamicId]});
Check("Unapproved discovered app is not restored",store.Load().SelectedApps.Count==0);
var readonlySnapshot=new AppSnapshot(new("process:test","Runtime","B",["bun.exe"],ExitMethod:ExitMethod.InspectOnly),[probe],true);
var readonlyResult=await new CleanupService(new Discovery()).CloseAsync(readonlySnapshot,null,CancellationToken.None,1);
Check("Read-only runtime cannot issue an exit request",!readonlySnapshot.CanClose&&readonlyResult.Status==ExitStatus.StillRunning);
var scan=new Discovery().Scan();
Check("Inventory includes the running test process",scan.Any(a=>a.Processes.Any(p=>p.Pid==Environment.ProcessId)));
Check("Inventory does not double count PIDs",scan.SelectMany(a=>a.Processes).GroupBy(p=>p.Pid).All(g=>g.Count()==1));
Check("All protected or inspection-only rows cannot close",scan.Where(a=>a.Definition.Protected||a.Definition.ExitMethod==ExitMethod.InspectOnly).All(a=>!a.CanClose));
var listener=new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback,0);
listener.Start();
int listenPort=((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
Check("TCP ownership accepts exact local listener",ClaudeMem.OwnsListener(Environment.ProcessId,listenPort));
Check("TCP ownership rejects another PID",!ClaudeMem.OwnsListener(Environment.ProcessId+1,listenPort));
listener.Stop();
Check("Closed TCP listener is not authorized",!ClaudeMem.OwnsListener(Environment.ProcessId,listenPort));
string? fixture=args.FirstOrDefault();
if(fixture is not null)
{
    var discovery=new Discovery();
    async Task<(Process Process,AppSnapshot Snapshot)> Start(string mode)
    {
        var process=Process.Start(new ProcessStartInfo(Path.GetFullPath(fixture),mode){UseShellExecute=false})!;
        process.WaitForInputIdle(5000);await Task.Delay(400);
        var identity=discovery.Inspect(process.Id)??throw new Exception("Cannot inspect own fixture");
        return(process,new AppSnapshot(new("fixture","Fixture","F",[identity.Name]),[identity],true));
    }
    var normal=await Start("normal");
    var outcome=await new CleanupService(discovery).CloseAsync(normal.Snapshot,null,CancellationToken.None,600);
    Check("Cooperative exit closes normal fixture",outcome.Status==ExitStatus.Closed,outcome);normal.Process.Dispose();
    var hidden=await Start("hidden");
    outcome=await new CleanupService(discovery).CloseAsync(hidden.Snapshot,null,CancellationToken.None,600);
    Check("Cooperative exit closes hidden/tray-style fixture",outcome.Status==ExitStatus.Closed,outcome);hidden.Process.Dispose();
    var denied=await Start("decline");
    outcome=await new CleanupService(discovery).CloseAsync(denied.Snapshot,null,CancellationToken.None,600);
    Check("App veto remains running",outcome.Status==ExitStatus.StillRunning&&!denied.Process.HasExited,outcome);
    var protectedApp=denied.Snapshot with{Definition=denied.Snapshot.Definition with{Protected=true}};
    outcome=await new CleanupService(discovery).CloseAsync(protectedApp,null,CancellationToken.None,600);
    Check("Protected fixture receives no shutdown",outcome.Status==ExitStatus.Protected&&!denied.Process.HasExited,outcome);
    outcome=await new CleanupService(discovery).CloseAsync(denied.Snapshot with{Complete=false},null,CancellationToken.None,600);
    Check("Incomplete identity skips shutdown",outcome.Status==ExitStatus.AccessUnavailable&&!denied.Process.HasExited,outcome);
    var fake=denied.Snapshot with{Processes=[denied.Snapshot.Processes[0] with{Created=1}]};
    outcome=await new CleanupService(discovery).CloseAsync(fake,null,CancellationToken.None,600);
    Check("Stale PID identity leaves real process untouched",outcome.Status==ExitStatus.AlreadyClosed&&!denied.Process.HasExited,outcome);
    var parent=await Start("normal");
    var leftover=denied.Snapshot.Processes[0] with{ParentPid=parent.Process.Id};
    var family=parent.Snapshot with{Processes=[parent.Snapshot.Processes[0],leftover]};
    outcome=await new CleanupService(discovery).CloseAsync(family,null,CancellationToken.None,600);
    Check("Remaining child prevents false success",outcome.Status==ExitStatus.StillRunning&&!denied.Process.HasExited&&parent.Process.HasExited,outcome);parent.Process.Dispose();
    var alreadyCanceled=new CancellationTokenSource();alreadyCanceled.Cancel();
    var canceledReport=await new CleanupService(discovery).RunAsync([denied.Snapshot],null,alreadyCanceled.Token);
    Check("Canceled run sends no new exit request",canceledReport.Apps.Single().Status==ExitStatus.Canceled&&!denied.Process.HasExited);
    // Only disposable test-owned processes are terminated for test teardown, never user applications.
    denied.Process.Kill();await denied.Process.WaitForExitAsync();denied.Process.Dispose();
}
File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"test-results.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions{WriteIndented=true}));
return failed==0?0:1;
