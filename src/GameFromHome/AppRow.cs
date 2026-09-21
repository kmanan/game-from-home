using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using GameFromHome.Core;
namespace GameFromHome;
public sealed class AppRow : INotifyPropertyChanged
{
    private bool selected; private bool canSelect=true; private string status=""; private string memoryText=""; private Brush statusBrush=new SolidColorBrush(Color.FromRgb(173,181,165));
    public required AppSnapshot Snapshot {get;set;}
    public string Id=>Snapshot.Id;
    public string Name=>Snapshot.Definition.Name;
    public long MemoryBytes=>Snapshot.Ram;
    public string Initials=>Snapshot.Definition.Initials;
    public string AccessibleName=>"Close "+Name;
    public string Details=>Snapshot.Context+"\n"+string.Join("\n",Snapshot.Processes.Select(p=>$"PID {p.Pid}: {p.Path}"));
    public ImageSource? AppIcon=>Snapshot.Processes.FirstOrDefault(p=>!string.IsNullOrEmpty(p.Path)) is {} p ? AppIcons.For(p.Path) : null;
    public bool Selected {get=>selected;set{selected=value;Changed();}}
    public bool CanSelect {get=>canSelect;set{canSelect=value;Changed();}}
    public string Status {get=>status;set{status=value;Changed();}}
    public string MemoryText {get=>memoryText;set{memoryText=value;Changed();}}
    public Brush StatusBrush {get=>statusBrush;set{statusBrush=value;Changed();}}
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName]string? name=null)=>PropertyChanged?.Invoke(this,new(name));
}
