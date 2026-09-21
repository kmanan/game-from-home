using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace GameFromHome;
internal static class AppIcons
{
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] private struct FileInfo{public nint Icon;public int Index;public uint Attributes;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)]public string Name;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=80)]public string Type;}
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)]private static extern nint SHGetFileInfo(string path,uint attributes,out FileInfo info,uint size,uint flags);
    [DllImport("user32.dll")]private static extern bool DestroyIcon(nint icon);
    private static readonly Dictionary<string,ImageSource?> Cache=new(StringComparer.OrdinalIgnoreCase);
    public static ImageSource? For(string path)
    {
        if(Cache.TryGetValue(path,out var cached))return cached;
        FileInfo info=default;
        try{if(SHGetFileInfo(path,0,out info,(uint)Marshal.SizeOf<FileInfo>(),0x100)==0||info.Icon==0)return null;var source=Imaging.CreateBitmapSourceFromHIcon(info.Icon,Int32Rect.Empty,BitmapSizeOptions.FromWidthAndHeight(32,32));source.Freeze();Cache[path]=source;return source;}
        catch{Cache[path]=null;return null;}
        finally{if(info.Icon!=0)DestroyIcon(info.Icon);}
    }
}
