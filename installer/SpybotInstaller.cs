using System;
using System.IO;
using System.Net;
using System.Diagnostics;
using Microsoft.Win32;
namespace Catalogo {
 public static class SpybotInstaller {
  public const string Url="https://updates2.safer-networking.org/spybot1/spybotsd-2.9.85.5.exe";
  public const string Hash="08d8e206d5baa738e4d50a7956984b84fccabdd36a0b7fe6b51b9fa74c4e623b";
  public static bool Supports(AppInfo a){return a!=null&&a.id=="spybot"&&a.architecture=="x64"&&String.IsNullOrEmpty(a.wingetId);}
  public static string Arguments(AppInfo a){if(!Supports(a))throw new InvalidDataException("Programa no autorizado.");return "/SP- /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /RESTARTEXITCODE=3010 /NOCLOSEAPPLICATIONS /NORESTARTAPPLICATIONS /TASKS=";}
  static bool Installed(bool exact){foreach(var view in new[]{RegistryView.Registry32,RegistryView.Registry64})using(var baseKey=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,view))using(var root=baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall")){if(root==null)continue;foreach(var n in root.GetSubKeyNames())using(var k=root.OpenSubKey(n)){var display=k==null?null:k.GetValue("DisplayName") as string;if(!String.IsNullOrEmpty(display)&&display.StartsWith("Spybot - Search & Destroy",StringComparison.OrdinalIgnoreCase)&&(!exact||String.Equals(k.GetValue("DisplayVersion") as string,"2.9.85.5",StringComparison.Ordinal)))return true;}}return false;}
  public static int Install(AppInfo a,Action<string> log){
   if(!Supports(a))throw new InvalidDataException("Programa no autorizado.");if(!Environment.Is64BitOperatingSystem||Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE")=="ARM64")throw new InvalidOperationException("Esta distribución requiere Windows x64.");
   if(Installed(false)){log("Spybot ya presente: se conserva sin actualizar.");return unchecked((int)0x8A150061);}
   var dir=Path.Combine(Path.GetTempPath(),"TodoInstala-Spybot-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);var file=Path.Combine(dir,"spybot.exe");
   try{ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;var req=(HttpWebRequest)WebRequest.Create(Url);req.Timeout=60000;req.ReadWriteTimeout=60000;req.UserAgent="TodoInstala/0.1";log("Descarga oficial: "+Url);
    using(var r=(HttpWebResponse)req.GetResponse()){if(r.ResponseUri.Scheme!="https")throw new InvalidDataException("Redirección insegura.");using(var input=r.GetResponseStream())using(var output=new FileStream(file,FileMode.CreateNew,FileAccess.Write,FileShare.None)){var b=new byte[65536];int n;long total=0;while((n=input.Read(b,0,b.Length))>0){total+=n;if(total>100L*1024*1024)throw new InvalidDataException("Descarga demasiado grande.");output.Write(b,0,n);}}}
    using(var verified=File.OpenRead(file)){if(!DirectInstaller.Verify(verified,Hash))throw new InvalidDataException("La descarga no coincide con la versión revisada.");log("SHA-256 verificado: "+Hash);
     using(var p=Process.Start(new ProcessStartInfo(file,Arguments(a)){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=dir})){if(p==null)throw new InvalidOperationException("No se pudo iniciar Spybot.");p.WaitForExit();int code=p.ExitCode;if(code!=0&&code!=3010)return code;if(!Installed(true))throw new InvalidOperationException("No se confirmó Spybot 2.9.85.5.");log("Spybot confirmado en Windows.");return code;}}
   }finally{try{File.Delete(file);Directory.Delete(dir,false);}catch(Exception e){log("Limpieza temporal pendiente: "+e.Message);}}
  }
 }
}
