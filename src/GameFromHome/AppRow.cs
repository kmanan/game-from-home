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
    public string Initials=>Snapshot.Definition.Initials;
    public string AccessibleName=>"Close "+Name;
    public ImageSource? AppIcon=>AppIcons.For(Snapshot.Processes.First(p=>Snapshot.Definition.Executables.Contains(p.Name,StringComparer.OrdinalIgnoreCase)).Path);
    public bool Selected {get=>selected;set{selected=value;Changed();}}
    public bool CanSelect {get=>canSelect;set{canSelect=value;Changed();}}
    public string Status {get=>status;set{status=value;Changed();}}
    public string MemoryText {get=>memoryText;set{memoryText=value;Changed();}}
    public Brush StatusBrush {get=>statusBrush;set{statusBrush=value;Changed();}}
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName]string? name=null)=>PropertyChanged?.Invoke(this,new(name));
}
