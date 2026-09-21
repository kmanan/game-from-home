using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using GameFromHome.Core;

namespace GameFromHome;

public partial class MainWindow : Window
{
    private const long MinimumVisibleRam = 500_000_000;
    private readonly Discovery discovery=new();
    private readonly ProfileStore store;
    private readonly Preferences preferences;
    private readonly ObservableCollection<AppRow> rows=[];
    private readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromSeconds(5)};
    private readonly bool autoRun;
    private readonly string? capture;
    private CancellationTokenSource? cancel;
    private bool busy, refreshing, finished, initialized, closeWhenDone;
    private CancellationTokenSource? autoExit;

    public MainWindow(ProfileStore store,bool autoRun,string? capture)
    {
        InitializeComponent(); this.store=store;this.autoRun=autoRun;this.capture=capture;preferences=store.Load();
        MaxWidth=SystemParameters.WorkArea.Width;MaxHeight=SystemParameters.WorkArea.Height;
        MinWidth=Math.Min(MinWidth,MaxWidth);MinHeight=Math.Min(MinHeight,MaxHeight);
        Width=Math.Min(Width,MaxWidth);Height=Math.Min(Height,MaxHeight);
        AutoExitCheck.IsChecked=preferences.AutoExit; AppList.ItemsSource=rows;
        var view=CollectionViewSource.GetDefaultView(rows);view.Filter=MatchesSearch;view.SortDescriptions.Add(new SortDescription(nameof(AppRow.MemoryBytes),ListSortDirection.Descending));
        PreferencesNote.Text="One-click shortcuts use your last approved app selection.";
        timer.Tick+=async(_,_)=>{if(WindowState!=WindowState.Minimized&&!busy&&!finished)await RefreshAsync(false);};
        Loaded+=async(_,_)=> {
            await RefreshAsync(true);initialized=true;
            if(store.LoadWarning is not null) ShowNotice(store.LoadWarning);
            if(capture is not null) { await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ContextIdle); Capture(capture);Close();return; }
            timer.Start(); if(autoRun && preferences.Configured && store.LoadWarning is null && rows.Any(r=>r.Selected)) await RunAsync();
            else if(autoRun)ShowNotice("Review the apps and press Free up RAM to establish your one-click selection.");
        };
        Closing+=OnClosing;
    }
    private async Task RefreshAsync(bool rebuild)
    {
        if(refreshing||busy)return;refreshing=true;
        try
        {
            var snapshots=await Task.Run(discovery.Scan);var memory=Discovery.ReadMemory();DisplayMemory(memory);
            var visible=snapshots.Where(s=>s.Ram>MinimumVisibleRam).ToList();
            if(rebuild)
            {
                rows.Clear(); foreach(var snap in visible)
                {
                    bool approved=preferences.ApprovedFingerprints.TryGetValue(snap.Id,out var fingerprint)&&fingerprint==snap.Fingerprint;
                    var row=new AppRow{Snapshot=snap,Selected=snap.CanClose&&(!preferences.Configured ? snap.Definition.DefaultSelected : preferences.SelectedApps.Contains(snap.Id)&&approved),CanSelect=snap.CanClose};
                    Populate(row);row.PropertyChanged+=RowChanged;rows.Add(row);
                    if(preferences.Configured&&preferences.SelectedApps.Contains(snap.Id)&&!approved)row.Status="App installation changed; select to approve again.";
                }
            }
            else
            {
                foreach(var row in rows.ToArray())
                {
                    var snap=visible.FirstOrDefault(a=>a.Id==row.Id);
                    if(snap is null) {rows.Remove(row);continue;}
                    if(row.Snapshot.Fingerprint!=snap.Fingerprint){row.Selected=false;row.Status="App installation changed. Refresh to review.";row.CanSelect=false;continue;}
                    row.Snapshot=snap;Populate(row);row.CanSelect=snap.CanClose;if(!snap.CanClose)row.Selected=false;
                }
                foreach(var snap in visible.Where(s=>rows.All(r=>r.Id!=s.Id))) {var row=new AppRow{Snapshot=snap,Selected=false,CanSelect=snap.CanClose};Populate(row);row.PropertyChanged+=RowChanged;rows.Add(row);}
            }
            EmptyText.Visibility=rows.Count==0?Visibility.Visible:Visibility.Collapsed;
            var protectedApps=snapshots.Where(s=>s.Definition.Protected && s.Ram>MinimumVisibleRam).Select(s=>$"{s.Definition.Name} stays open · {(s.MemoryComplete?"":"at least ")}{Formatting.Ram(s.Ram)}");
            ProtectedText.Text=string.Join("     •     ",protectedApps);
            if(string.IsNullOrEmpty(ProtectedText.Text))ProtectedText.Text="Discord stays protected. Codex is optional and closes last when selected.";

            CollectionViewSource.GetDefaultView(rows).Refresh();
            UpdateSummary();
        }
        catch(Exception ex){ShowNotice("Could not refresh app details: "+ex.Message);PrimaryButton.IsEnabled=false;}
        finally{refreshing=false;}
    }
    private bool MatchesSearch(object value)
    {
        if(value is not AppRow row)return false;
        var text=SearchBox.Text.Trim();
        return text.Length==0 || row.Name.Contains(text,StringComparison.OrdinalIgnoreCase) || row.Details.Contains(text,StringComparison.OrdinalIgnoreCase);
    }
    private void SearchChanged(object sender,TextChangedEventArgs e)
    {
        if(initialized)CollectionViewSource.GetDefaultView(rows).Refresh();
    }
    private static void Populate(AppRow row)
    {
        var snap=row.Snapshot;
        row.MemoryText=snap.Processes.All(p=>p.PrivateWorkingSet is null)?"Unknown":(snap.MemoryComplete?"":"≥ ")+Formatting.AppRam(snap.Ram);
        row.Status=snap.Definition.Protected?"Protected · stays open":snap.Definition.ExitMethod==ExitMethod.InspectOnly?snap.Context:!snap.Complete?$"{snap.UnreadableCount} process identities unavailable; skipped":snap.Id=="claude-mem"?"Stop worker through its own API · may restart when used":snap.Id=="codex"?$"{snap.Processes.Count} processes · optional; interrupts active tasks":$"{snap.Processes.Count} processes · clean exit request";
        row.StatusBrush=new SolidColorBrush(Color.FromRgb(173,181,165));
    }
    private void RowChanged(object? sender,PropertyChangedEventArgs e){if(e.PropertyName==nameof(AppRow.Selected)&&!busy&&!finished)UpdateSummary();}
    private void UpdateSummary()
    {
        if(busy||finished)return;var selected=rows.Where(r=>r.Selected&&r.CanSelect).ToArray();
        Summary.Text=$"{selected.Length} apps selected · {Formatting.Ram(selected.Sum(r=>r.Snapshot.Ram))} in use";
        SummaryNote.Text="Select apps to close. Grey rows show memory but have no clean-exit action.";PrimaryButton.IsEnabled=selected.Length>0;
    }
    private void DisplayMemory(MemorySample sample)
    {
        AvailableText.Text=$"{sample.Available/Formatting.GiB:F1} GiB";TotalText.Text=$"available of {sample.Total/Formatting.GiB:F1} GiB";
        UsedText.Text=$"{(sample.Total-sample.Available)/Formatting.GiB:F1} GiB in use · {(sample.Total-sample.Available)*100d/sample.Total:F0}%";
        UsedColumn.Width=new GridLength(Math.Max(1,sample.Total-sample.Available),GridUnitType.Star);FreeColumn.Width=new GridLength(Math.Max(1,sample.Available),GridUnitType.Star);
    }
    private async Task RunAsync()
    {
        if(busy||refreshing)return;var selected=rows.Where(r=>r.Selected&&r.CanSelect).ToArray();if(selected.Length==0)return;
        var approved=selected.Select(r=>r.Snapshot).ToArray();
        preferences.Configured=true;
        var absent=preferences.SelectedApps.Where(id=>rows.All(r=>r.Id!=id));
        preferences.SelectedApps=absent.Concat(selected.Select(r=>r.Id)).Distinct().ToList();
        foreach(var snap in approved)preferences.ApprovedFingerprints[snap.Id]=snap.Fingerprint;
        try{store.Save(preferences);}catch(Exception ex){ShowNotice("Could not save your selection: "+ex.Message);return;}
        busy=true;finished=false;timer.Stop();autoExit?.Cancel();cancel=new();
        foreach(var row in rows){row.CanSelect=false;if(!row.Selected && row.Snapshot.CanClose)row.Status="Kept open";}
        RefreshButton.IsEnabled=false;PreferencesButton.IsEnabled=false;PreferencesPanel.Visibility=Visibility.Collapsed;Notice.Visibility=Visibility.Collapsed;
        StopButton.Visibility=Visibility.Visible;StopButton.IsEnabled=true;PrimaryButton.IsEnabled=false;PrimaryButton.Content="Closing apps…";
        Headline.Text="Wrapping things up.";Subtitle.Text="Giving each app time to exit cleanly.";Eyebrow.Text="FREEING UP RAM";
        try
        {
            var progress=new Progress<(string AppId,string Message)>(p=>{var row=rows.FirstOrDefault(r=>r.Id==p.AppId);if(row is not null)row.Status=p.Message;Summary.Text=p.Message;});
            var report=await new CleanupService(discovery).RunAsync(approved,progress,cancel.Token);
            foreach(var result in report.Apps)
            {
                var row=rows.First(r=>r.Id==result.AppId);row.Status=result.Detail;
                bool closed=result.Status is ExitStatus.Closed or ExitStatus.AlreadyClosed;
                row.MemoryText=closed?"Closed":result.RemainingRam>0?Formatting.AppRam(result.RemainingRam):row.MemoryText;
                row.StatusBrush=new SolidColorBrush(closed?Color.FromRgb(171,214,168):Color.FromRgb(255,189,131));
            }
            int closedCount=report.Apps.Count(a=>a.Status==ExitStatus.Closed);int remainder=report.Apps.Count(a=>a.Status is not (ExitStatus.Closed or ExitStatus.AlreadyClosed));
            Headline.Text=report.AllClosed?"Room to play.":"Some apps stayed open.";
            Subtitle.Text=$"{closedCount} apps closed cleanly. Discord was left untouched.";
            Eyebrow.Text=report.AllClosed?"YOU’RE ALL SET":"FINISHED WITH EXCEPTIONS";ListTitle.Text="Your cleanup results";
            if(report.After is not null)DisplayMemory(report.After);
            Summary.Text=report.Delta is long delta?Formatting.Delta(delta):"RAM change unavailable";
            SummaryNote.Text=report.Before is not null&&report.After is not null?$"Before {report.Before.Available/Formatting.GiB:F1} GiB → after {report.After.Available/Formatting.GiB:F1} GiB · measured change":"Exit results verified independently of RAM measurement.";
            if(remainder>0)ShowNotice($"{remainder} apps could not be confirmed closed. Use Show to review an app, then Refresh to try again.");
            try{store.SaveReport(report);}catch(Exception ex){ShowNotice("Cleanup finished, but its report could not be saved: "+ex.Message);}
            finished=true;
            if(preferences.AutoExit&&report.AllClosed&&!closeWhenDone){autoExit=new();_ = CloseLaterAsync(autoExit.Token);}
        }
        catch(Exception ex){ShowNotice("Cleanup stopped: "+ex.Message);finished=true;Summary.Text="Review app status before retrying.";}
        finally
        {
            busy=false;StopButton.Visibility=Visibility.Collapsed;PrimaryButton.Content="Done";PrimaryButton.IsEnabled=true;RefreshButton.IsEnabled=true;PreferencesButton.IsEnabled=true;cancel.Dispose();cancel=null;
            if(closeWhenDone)Close();
        }
    }
    private async Task CloseLaterAsync(CancellationToken token){try{await Task.Delay(8000,token);Close();}catch(OperationCanceledException){}}
    private async void PrimaryClick(object sender,RoutedEventArgs e){if(finished)Close();else await RunAsync();}
    private async void RefreshClick(object sender,RoutedEventArgs e){autoExit?.Cancel();finished=false;Headline.Text="Make room for play.";Subtitle.Text="Quit your everyday apps in one go.";Eyebrow.Text="WORK WRAPPED. PLAY NEXT.";ListTitle.Text="Using over 500 MB";PrimaryButton.Content="Free up RAM";Notice.Visibility=Visibility.Collapsed;await RefreshAsync(true);timer.Start();}
    private void StopClick(object sender,RoutedEventArgs e){cancel?.Cancel();StopButton.IsEnabled=false;Summary.Text="Stopping new requests; verifying any exits already requested…";}
    private void PreferencesClick(object sender,RoutedEventArgs e){autoExit?.Cancel();PreferencesPanel.Visibility=PreferencesPanel.Visibility==Visibility.Visible?Visibility.Collapsed:Visibility.Visible;}
    private void AutoExitChanged(object sender,RoutedEventArgs e){if(!initialized)return;preferences.AutoExit=AutoExitCheck.IsChecked==true;try{store.Save(preferences);}catch(Exception ex){ShowNotice("Could not save preference: "+ex.Message);}}
    private void ShowAppClick(object sender,RoutedEventArgs e){autoExit?.Cancel();if(sender is Button {DataContext:AppRow row}&&!Discovery.ShowApp(row.Snapshot))ShowNotice(row.Name+" has no visible app window. Its notification-area menu may still be available.");}
    private void ShowNotice(string message){Notice.Text=message;Notice.Visibility=Visibility.Visible;}
    private void OnClosing(object? sender,CancelEventArgs e){if(busy){e.Cancel=true;closeWhenDone=true;cancel?.Cancel();Summary.Text="Finishing exit verification before closing…";}else{timer.Stop();autoExit?.Cancel();}}
    private void Capture(string path){UpdateLayout();var image=new RenderTargetBitmap((int)ActualWidth,(int)ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(this);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);using var file=File.Create(path);encoder.Save(file);}
    private void ShortcutClick(object sender,RoutedEventArgs e)
    {
        if(!preferences.Configured){PreferencesNote.Text="Run Free up RAM once to approve your selection before creating the shortcut.";return;}
        object? shell=null,shortcut=null;
        try
        {
            string executable=Environment.ProcessPath!;string path=Path.Combine(AppContext.BaseDirectory,"Free up RAM.lnk");
            var type=Type.GetTypeFromProgID("WScript.Shell")??throw new InvalidOperationException("Windows shortcut service unavailable.");
            shell=Activator.CreateInstance(type)!;dynamic automation=shell;shortcut=automation.CreateShortcut(path);dynamic link=shortcut;
            link.TargetPath=executable;link.Arguments="--run-profile default --show-result";link.WorkingDirectory=AppContext.BaseDirectory;link.Description="Cleanly quit the approved apps and show available RAM.";link.IconLocation=executable+",0";link.Save();
            PreferencesNote.Text="Created Free up RAM.lnk beside the app. It is ready for a desktop or Stream Deck launcher.";
            Process.Start(new ProcessStartInfo("explorer.exe","/select,\""+path+"\""){UseShellExecute=true});
        }
        catch(Exception ex){PreferencesNote.Text="Could not create shortcut: "+ex.Message;}
        finally{if(shortcut is not null)Marshal.FinalReleaseComObject(shortcut);if(shell is not null)Marshal.FinalReleaseComObject(shell);}
    }
}
