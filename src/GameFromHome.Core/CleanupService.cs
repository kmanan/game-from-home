namespace GameFromHome.Core;

public sealed class CleanupService(Discovery discovery)
{
    public async Task<CleanupReport> RunAsync(IReadOnlyList<AppSnapshot> selected, IProgress<(string AppId,string Message)>? progress, CancellationToken cancellationToken)
    {
        var start=DateTimeOffset.UtcNow; var before=await Discovery.SampleMedianAsync(); var results=new List<ExitResult>();
        using var total=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); total.CancelAfter(TimeSpan.FromSeconds(120));
        foreach(var app in selected.OrderBy(a=>a.Id=="codex" ? 1 : 0))
        {
            if(total.IsCancellationRequested) { results.Add(new(app.Id,app.Definition.Name,ExitStatus.Canceled,"Not requested.")); continue; }
            progress?.Report((app.Id,"Requesting clean exit…"));
            ExitResult result;
            try { result=await CloseAsync(app,progress,total.Token); }
            catch(OperationCanceledException) { result=new(app.Id,app.Definition.Name,ExitStatus.Canceled,"Exit request canceled; review current app state."); }
            catch(Exception ex) { result=new(app.Id,app.Definition.Name,ExitStatus.Failed,$"Could not complete exit ({ex.GetType().Name})."); }
            results.Add(result); progress?.Report((app.Id,result.Detail));
        }
        return new(start,DateTimeOffset.UtcNow,before,await Discovery.SampleMedianAsync(),results);
    }

    public async Task<ExitResult> CloseAsync(AppSnapshot app,IProgress<(string AppId,string Message)>? progress,CancellationToken token,int observeMs=5000)
    {
        ExitResult Result(ExitStatus status,string detail,int? code=null,long ram=0)=>new(app.Id,app.Definition.Name,status,detail,code,ram);
        if(app.Definition.Protected || app.Processes.Any(p=>Catalog.ProtectedPath(p.Path) || p.Pid==Environment.ProcessId)) return Result(ExitStatus.Protected,"Protected; no exit request sent.");
        if(app.Definition.ExitMethod==ExitMethod.InspectOnly) return Result(ExitStatus.StillRunning,"No verified clean-exit method for this process. No request sent.");
        if(!app.Complete) return Result(ExitStatus.AccessUnavailable,"Some app process identities are unavailable. No request sent.");
        var living=new List<ProcessIdentity>();
        foreach(var process in app.Processes)
        {
            var alive=Discovery.IsAlive(process);
            if(alive is null) return Result(ExitStatus.AccessUnavailable,"Process access changed. No request sent.");
            if(alive==true) { if(!discovery.Matches(process)) return Result(ExitStatus.AccessUnavailable,"App identity changed. Refresh before closing."); living.Add(process); }
        }
        if(living.Count==0) return Result(ExitStatus.AlreadyClosed,"Already closed.");
        var ids=living.Select(p=>p.Pid).ToHashSet();
        var roots=living.Where(p=>app.Definition.Executables.Contains(p.Name,StringComparer.OrdinalIgnoreCase) && !ids.Contains(p.ParentPid)).ToArray();
        if(roots.Length==0) return Result(ExitStatus.StillRunning,"Only background helpers remain; no safe app exit target.");
        using var request=CancellationTokenSource.CreateLinkedTokenSource(token); request.CancelAfter(TimeSpan.FromSeconds(35));
        var response=app.Definition.ExitMethod==ExitMethod.ClaudeMem
            ? await ClaudeMem.RequestAsync(app,discovery,request.Token)
            : await Task.Run(()=>RestartManager.Request(roots,request.Token),CancellationToken.None);
        progress?.Report((app.Id,"Verifying app processes…"));
        // Observe even after cancellation: cancellation does not imply that an earlier exit was undone.
        var until=DateTimeOffset.UtcNow.AddMilliseconds(observeMs); IReadOnlyList<AppSnapshot> latest=[];
        do { await Task.Delay(250); latest=await Task.Run(discovery.Scan); } while(DateTimeOffset.UtcNow<until);
        var states=app.Processes.Select(Discovery.IsAlive).ToArray();
        var current=latest.FirstOrDefault(a=>a.Id==app.Id);
        // A discovered GUI app may relaunch without a visible window, becoming an inspection-only row.
        if(current is null && app.Id.StartsWith("app:",StringComparison.Ordinal))
            current=latest.FirstOrDefault(a=>a.Processes.Any(p=>app.Processes.Any(old=>string.Equals(old.Path,p.Path,StringComparison.OrdinalIgnoreCase))));
        if(app.Definition.ExitMethod!=ExitMethod.ClaudeMem && app.Definition.Executables.Any(n=>discovery.InaccessibleNames.GetValueOrDefault(n)>0))return Result(ExitStatus.AccessUnavailable,"A current app process could not be identified; exit cannot be confirmed.",response.Code,current?.Ram??0);
        if(states.Any(s=>s is null)) return Result(ExitStatus.AccessUnavailable,"Could not verify every app process.",response.Code,current?.Ram??0);
        if(states.All(s=>s==false) && current is null) return Result(ExitStatus.Closed,"Exited cleanly.",response.Code);
        if(states.All(s=>s==false) && current is not null) return Result(ExitStatus.Relaunched,"App restarted after exit; left running.",response.Code,current.Ram);
        return Result(response.Code==5?ExitStatus.AccessUnavailable:ExitStatus.StillRunning,response.Code!=0?response.Detail:"Still running; no forced shutdown used.",response.Code,current?.Ram??0);
    }
}
