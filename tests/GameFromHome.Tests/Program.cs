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
Check("Protected and unknown IDs stripped from saved profiles",store.Load().SelectedApps.SequenceEqual(["edge"]));
File.WriteAllText(Path.Combine(profilePath,"preferences.json"),"broken");Check("Corrupt profile fails into review",!store.Load().Configured&&store.LoadWarning is not null);
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
