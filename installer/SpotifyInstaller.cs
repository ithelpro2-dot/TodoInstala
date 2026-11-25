using System;
using System.IO;
using System.Net;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Principal;
using Microsoft.Win32;

namespace Catalogo {
 // Experimental: Spotify installer must run as the original, non-elevated user.
 public static class SpotifyInstaller {
  public const string Url="https://download.scdn.co/SpotifyFullSetupX64.exe";
  public const string Hash="a92da3465625a868187cb2cf46e7fbeb91df990c0eaaab279d04f0e14d559667";
  const string Key=@"Software\Microsoft\Windows\CurrentVersion\Uninstall\Spotify";
  public static bool Supports(string id,string arch,string winget){return id=="spotify"&&arch=="x64"&&String.IsNullOrEmpty(winget);}
  static string Folder(){return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Spotify");}
  public static bool Present(){
   using(var key=Registry.CurrentUser.OpenSubKey(Key))if(key!=null)return true;
   var folder=Folder();if(!Directory.Exists(folder))return false;
   if((File.GetAttributes(folder)&FileAttributes.ReparsePoint)!=0)return true;
   using(var entries=Directory.EnumerateFileSystemEntries(folder).GetEnumerator())return entries.MoveNext();
  }
  static bool Confirmed(){
   using(var key=Registry.CurrentUser.OpenSubKey(Key))return key!=null&&(key.GetValue("DisplayVersion") as string)=="1.3.8.620.g39b276d5d7ac"&&File.Exists(Path.Combine(Folder(),"Spotify.exe"));
  }
  public static int Install(Action<string> log){
   using(var identity=WindowsIdentity.GetCurrent())if(new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))throw new InvalidOperationException("Spotify requiere la sesión original sin elevación.");
   if(!Environment.Is64BitOperatingSystem||Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE")=="ARM64")throw new InvalidOperationException("Esta distribución requiere Windows x64.");
   if(Present()){log("Spotify ya presente: se conserva sin actualizar.");return unchecked((int)0x8A150061);}
   var dir=Path.Combine(Path.GetTempPath(),"TodoInstala-Spotify-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
   var file=Path.Combine(dir,"SpotifyFullSetupX64.exe");
   try{
    ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
    var req=(HttpWebRequest)WebRequest.Create(Url);req.UserAgent="TodoInstala/0.1";req.Timeout=60000;req.ReadWriteTimeout=60000;
    log("Descarga oficial: "+Url);
    using(var response=(HttpWebResponse)req.GetResponse()){
     if(response.ResponseUri.Scheme!="https")throw new InvalidDataException("Redirección insegura.");
     using(var input=response.GetResponseStream())using(var output=new FileStream(file,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
      var b=new byte[65536];int n;long total=0;while((n=input.Read(b,0,b.Length))>0){total+=n;if(total>250L*1024*1024)throw new InvalidDataException("Descarga demasiado grande.");output.Write(b,0,n);}
     }
    }
    using(var verified=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read)){
     using(var sha=SHA256.Create())if(BitConverter.ToString(sha.ComputeHash(verified)).Replace("-","").ToLowerInvariant()!=Hash)throw new InvalidDataException("La descarga no coincide con la versión revisada de Spotify.");
     log("SHA-256 verificado: "+Hash);
     using(var p=Process.Start(new ProcessStartInfo(file,"/silent /skip-app-launch"){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=dir})){
      if(p==null)throw new InvalidOperationException("No se pudo iniciar Spotify.");p.WaitForExit();var code=p.ExitCode;if(code!=0)return code;
      if(!Confirmed())throw new InvalidOperationException("No se confirmó la versión revisada de Spotify en la cuenta original.");
      log("Spotify confirmado en la cuenta original.");return 0;
     }
    }
   }finally{try{File.Delete(file);Directory.Delete(dir,false);}catch(Exception e){log("Limpieza temporal pendiente: "+e.Message);}}
  }
 }
}
