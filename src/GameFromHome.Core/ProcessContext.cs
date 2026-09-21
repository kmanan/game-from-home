using System.Runtime.InteropServices;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace GameFromHome.Core;

public static class ProcessContext
{
    public static string? LastQueryError { get; private set; }
    public static bool IsRuntime(string name) => new[] { "bun.exe", "node.exe", "python.exe", "pythonw.exe", "pwsh.exe", "powershell.exe", "cmd.exe", "wsl.exe", "conhost.exe", "claude.exe", "codex.exe", "uv.exe", "uvx.exe" }.Contains(name, StringComparer.OrdinalIgnoreCase);
    public static string AppId(string path) => "app:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(path.ToLowerInvariant())));
    public static bool IsClaudeMemWorker(string name, string? command)
    {
        if (!new[] { "bun.exe", "node.exe" }.Contains(name, StringComparer.OrdinalIgnoreCase) || command is null) return false;
        var normalized = command.Replace('/', '\\');
        return normalized.Contains("\\claude-mem\\", StringComparison.OrdinalIgnoreCase)
            && normalized.Contains("\\scripts\\worker-service.cjs", StringComparison.OrdinalIgnoreCase);
    }

    private static object? ReadProperty(object record,string name)
    {
        object? properties=null,property=null;
        try
        {
            properties=record.GetType().InvokeMember("Properties_",BindingFlags.GetProperty,null,record,null)!;
            property=properties.GetType().InvokeMember("Item",BindingFlags.InvokeMethod,null,properties,[name,0])!;
            return property.GetType().InvokeMember("Value",BindingFlags.GetProperty,null,property,null);
        }
        finally { if(property is not null)Marshal.FinalReleaseComObject(property);if(properties is not null)Marshal.FinalReleaseComObject(properties); }
    }

    public static Dictionary<int,long> ReadPrivateWorkingSets()
    {
        var result=new Dictionary<int,long>();object? locator=null,connection=null,records=null;
        try
        {
            var type=Type.GetTypeFromProgID("WbemScripting.SWbemLocator");if(type is null)return result;
            locator=Activator.CreateInstance(type)!;
            connection=locator.GetType().InvokeMember("ConnectServer",BindingFlags.InvokeMethod,null,locator,[".","root\\cimv2"])!;
            // Cached enumeration is required: forward-only WMI enumerators reject COM Reset().
            records=connection.GetType().InvokeMember("ExecQuery",BindingFlags.InvokeMethod,null,connection,["SELECT IDProcess,WorkingSetPrivate FROM Win32_PerfFormattedData_PerfProc_Process","WQL",0])!;
            foreach(object record in (System.Collections.IEnumerable)records)
            {
                try { int pid=Convert.ToInt32(ReadProperty(record,"IDProcess"));if(pid>0)result[pid]=Convert.ToInt64(ReadProperty(record,"WorkingSetPrivate")); }
                finally {Marshal.FinalReleaseComObject(record);}
            }
        }
        catch(Exception e) when(e is COMException or UnauthorizedAccessException or ArgumentException or TargetInvocationException or InvalidCastException) { }
        finally {if(records is not null)Marshal.FinalReleaseComObject(records);if(connection is not null)Marshal.FinalReleaseComObject(connection);if(locator is not null)Marshal.FinalReleaseComObject(locator);}
        return result;
    }

    // Read only runtime command lines. They are transient and never written to diagnostics or profiles.
    public static Dictionary<int, string> ReadRuntimeCommands()
    {
        var result = new Dictionary<int, string>();LastQueryError=null;
        object? locator = null, connection = null, records = null;
        string stage="locator";
        try
        {
            var type = Type.GetTypeFromProgID("WbemScripting.SWbemLocator");
            if (type is null) return result;
            locator = Activator.CreateInstance(type);
            stage="connect";
            connection = locator!.GetType().InvokeMember("ConnectServer",BindingFlags.InvokeMethod,null,locator,[".", "root\\cimv2"])!;
            stage="query";
            records = connection.GetType().InvokeMember("ExecQuery",BindingFlags.InvokeMethod,null,connection,["SELECT ProcessId,CommandLine FROM Win32_Process WHERE Name='bun.exe' OR Name='node.exe'", "WQL", 0])!;
            stage="enumerate";
            foreach (object record in (System.Collections.IEnumerable)records)
            {
                try { string? command = ReadProperty(record,"CommandLine") as string; if (command is not null) result[Convert.ToInt32(ReadProperty(record,"ProcessId"))] = command; }
                finally { Marshal.FinalReleaseComObject(record); }
            }
        }
        catch (Exception e) when (e is COMException or UnauthorizedAccessException or ArgumentException or TargetInvocationException or InvalidCastException) { LastQueryError=stage+" "+e.GetType().Name+": "+e.Message; }
        finally
        {
            if (records is not null) Marshal.FinalReleaseComObject(records);
            if (connection is not null) Marshal.FinalReleaseComObject(connection);
            if (locator is not null) Marshal.FinalReleaseComObject(locator);
        }
        return result;
    }
}
