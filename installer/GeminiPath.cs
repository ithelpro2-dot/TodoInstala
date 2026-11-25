using System;
using System.IO;
using System.Linq;
using Microsoft.Win32;
namespace Catalogo {
 // Preparación experimental: no se invoca desde el instalador público.
 public static class GeminiPath {
  [System.Runtime.InteropServices.DllImport("user32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode,SetLastError=true)] static extern IntPtr SendMessageTimeout(IntPtr hwnd,uint msg,UIntPtr wp,string lp,uint flags,uint timeout,out UIntPtr result);
  public static bool Notify(){UIntPtr result;return SendMessageTimeout(new IntPtr(0xffff),0x001A,UIntPtr.Zero,"Environment",2,5000,out result)!=IntPtr.Zero;}
  static void Validate(string entry){if(String.IsNullOrWhiteSpace(entry)||entry.IndexOf(';')>=0||entry.IndexOf('\0')>=0||!Path.IsPathRooted(entry))throw new InvalidDataException("Ruta de lanzador no válida.");}
  static bool Equal(string a,string b){return String.Equals(Environment.ExpandEnvironmentVariables(a.Trim().Trim('"')).TrimEnd('\\','/'),Environment.ExpandEnvironmentVariables(b.Trim().Trim('"')).TrimEnd('\\','/'),StringComparison.OrdinalIgnoreCase);}
  public static bool Contains(string value,string entry){Validate(entry);return (value??"").Split(';').Any(s=>Equal(s,entry));}
  public static string Add(string value,string entry){Validate(entry);value=value??"";if(Contains(value,entry))return value;var result=value.Length==0?entry:value.EndsWith(";",StringComparison.Ordinal)?value+entry:value+";"+entry;if(result.Length>32760)throw new InvalidDataException("La variable PATH no admite más caracteres.");return result;}
  // Retira solamente una entrada que TodoInstala añadió y cuya presencia se registró.
  public static string RemoveOwned(string value,string entry,bool owned){Validate(entry);if(!owned||String.IsNullOrEmpty(value))return value;var parts=value.Split(';').ToList();var matches=parts.Select((s,i)=>new {s,i}).Where(x=>Equal(x.s,entry)).ToArray();if(matches.Length==1)parts.RemoveAt(matches[0].i);return String.Join(";",parts);}
  public static bool Register(string entry){return Register(entry,"gemini.cmd");}
  public static bool Register(string entry,string command){
   if(command!="gemini.cmd"&&command!="cline.exe"&&command!="openclaw.cmd")throw new InvalidDataException("Comando no autorizado.");
   Validate(entry);if(!Directory.Exists(entry)||(File.GetAttributes(entry)&FileAttributes.ReparsePoint)!=0||!File.Exists(Path.Combine(entry,command)))throw new InvalidDataException("Falta el lanzador preparado.");
   using(var key=Registry.CurrentUser.CreateSubKey("Environment")){
    var raw=key.GetValue("Path",null,RegistryValueOptions.DoNotExpandEnvironmentNames);if(raw!=null&&!(raw is string))throw new InvalidDataException("PATH tiene un tipo no compatible.");
    string old=(string)raw;var kind=raw==null?RegistryValueKind.ExpandString:key.GetValueKind("Path");if(kind!=RegistryValueKind.String&&kind!=RegistryValueKind.ExpandString)throw new InvalidDataException("PATH tiene un tipo no compatible.");
    if(Contains(old,entry))return false;string next=Add(old,entry);
    key.SetValue("Path",next,kind);return true;
   }
  }
 }
}
