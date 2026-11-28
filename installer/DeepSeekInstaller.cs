using System;
using System.IO;
using System.Net;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Principal;
using Microsoft.Win32;

namespace Catalogo {
 // User-scoped route; not registered in the public installer yet.
 public static class DeepSeekInstaller {
  public const string Url="https://download.deepseek.com/desktop/dsh-latest-windows-x64.exe";
  public const string Hash="830947f8996dc05d991ca9133fff4d3400073e47b9cfa6507754867e79482b23";
  const string Uninstall=@"Software\Microsoft\Windows\CurrentVersion\Uninstall\1bf39983-50d0-5fe0-9ef4-cece76f67c5e";
  public static bool Supports(string id,string arch,string wingetId){return id=="deepseek-harness"&&arch=="x64"&&String.IsNullOrEmpty(wingetId);}
  static bool Installed(bool exact){
   using(var key=Registry.CurrentUser.OpenSubKey(Uninstall)){
    if(key==null||!String.Equals(key.GetValue("DisplayName") as string,"DeepSeek Harness",StringComparison.Ordinal))return false;
    if(!exact)return true;
    var version=key.GetValue("DisplayVersion") as string;
    var location=key.GetValue("InstallLocation") as string;
    return version=="0.2.2-rc.1"&&!String.IsNullOrEmpty(location)&&Directory.Exists(location);
   }
  }
  public static int Install(Action<string> log){
   using(var identity=WindowsIdentity.GetCurrent())if(new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))
    throw new InvalidOperationException("DeepSeek debe instalarse en la sesión original sin elevación.");
   if(!Environment.Is64BitOperatingSystem||Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE")=="ARM64")throw new InvalidOperationException("Esta distribución requiere Windows x64.");
   if(Installed(false)){log("DeepSeek Harness ya presente: se conserva sin actualizar.");return unchecked((int)0x8A150061);}
   var dir=Path.Combine(Path.GetTempPath(),"TodoInstala-DeepSeek-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
   var file=Path.Combine(dir,"deepseek.exe");
   try{
    ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
    var request=(HttpWebRequest)WebRequest.Create(Url);request.UserAgent="TodoInstala/0.1";request.Timeout=60000;request.ReadWriteTimeout=60000;
    log("Descarga oficial: "+Url);
    using(var response=InstallationWork.Receive(request)){
     if(response.ResponseUri.Scheme!="https")throw new InvalidDataException("Redirección de descarga no segura.");
     using(var input=response.GetResponseStream())using(var output=new FileStream(file,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
      var buffer=new byte[65536];int n;long total=0;while((n=input.Read(buffer,0,buffer.Length))>0){InstallationWork.Check();total+=n;if(total>400L*1024*1024)throw new InvalidDataException("Descarga demasiado grande.");output.Write(buffer,0,n);}
     }
    }
    using(var verified=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read)){
     using(var sha=SHA256.Create())if(BitConverter.ToString(sha.ComputeHash(verified)).Replace("-","").ToLowerInvariant()!=Hash)
      throw new InvalidDataException("La descarga de DeepSeek no coincide con la versión revisada.");
     log("SHA-256 verificado: "+Hash);
     using(var p=InstallationWork.StartInstaller(new ProcessStartInfo(file,"/S"){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=dir})){
      if(p==null)throw new InvalidOperationException("No se pudo iniciar DeepSeek.");p.WaitForExit();var code=p.ExitCode;
      if(code!=0&&code!=3010)return code;
      if(!Installed(true))throw new InvalidOperationException("No se confirmó DeepSeek Harness 0.2.2-rc.1 en la cuenta de usuario.");
      log("Instalación confirmada en la cuenta de usuario.");return code;
     }
    }
   }finally{try{File.Delete(file);Directory.Delete(dir,false);}catch(Exception e){log("Limpieza temporal pendiente: "+e.Message);}}
  }
 }
}
