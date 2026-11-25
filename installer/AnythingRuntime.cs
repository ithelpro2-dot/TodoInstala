using System;
using System.IO;
using System.Net;
using System.Diagnostics;
using Microsoft.Win32;

namespace Catalogo {
 // Machine prerequisite for the reviewed AnythingLLM 1.19.0 installer.
 public static class AnythingRuntime {
  public const string Url="https://download.visualstudio.microsoft.com/download/pr/73aabf2e-9532-4f68-99f7-3247081a619c/CC0FF0EB1DC3F5188AE6300FAEF32BF5BEEBA4BDD6E8E445A9184072096B713B/VC_redist.x64.exe";
  public const string Hash="cc0ff0eb1dc3f5188ae6300faef32bf5beeba4bdd6e8e445a9184072096b713b";
  public static readonly Version Minimum=new Version(14,44,35211,0);
  public static bool IsSufficient(int installed,int major,int minor,int build,int revision){
   return installed==1&&major>=0&&minor>=0&&build>=0&&revision>=0&&new Version(major,minor,build,revision)>=Minimum;
  }
  static int Number(RegistryKey k,string n){var v=k.GetValue(n);return v is int?(int)v:-1;}
  public static bool Installed(){
   using(var root=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64))
   using(var key=root.OpenSubKey(@"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64"))
    return key!=null&&IsSufficient(Number(key,"Installed"),Number(key,"Major"),Number(key,"Minor"),Number(key,"Bld"),Number(key,"Rbld"));
  }
  public static int Ensure(Action<string> log){
   if(!Environment.Is64BitOperatingSystem||Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE")=="ARM64")throw new InvalidOperationException("Esta distribución requiere Windows x64.");
   if(Installed()){log("Microsoft Visual C++ x64 ya cumple el requisito.");return 0;}
   var dir=Path.Combine(Path.GetTempPath(),"TodoInstala-VC-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
   var file=Path.Combine(dir,"vc_redist.x64.exe");
   try{
    ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
    var request=(HttpWebRequest)WebRequest.Create(Url);request.UserAgent="TodoInstala/0.1";request.Timeout=60000;request.ReadWriteTimeout=60000;
    log("Descarga oficial del componente de Microsoft: "+Url);
    using(var response=(HttpWebResponse)request.GetResponse()){
     if(response.ResponseUri.Scheme!="https")throw new InvalidDataException("Redirección insegura.");
     using(var input=response.GetResponseStream())using(var output=new FileStream(file,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
      var b=new byte[65536];int n;long total=0;while((n=input.Read(b,0,b.Length))>0){total+=n;if(total>50L*1024*1024)throw new InvalidDataException("Descarga demasiado grande.");output.Write(b,0,n);}
     }
    }
    using(var verified=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read)){
     using(var sha=System.Security.Cryptography.SHA256.Create())if(BitConverter.ToString(sha.ComputeHash(verified)).Replace("-","").ToLowerInvariant()!=Hash)throw new InvalidDataException("El componente de Microsoft no coincide con la huella revisada.");
     log("SHA-256 del componente de Microsoft verificado: "+Hash);
     using(var p=Process.Start(new ProcessStartInfo(file,"/install /quiet /norestart"){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=dir})){
      if(p==null)throw new InvalidOperationException("No se pudo iniciar el componente de Microsoft.");p.WaitForExit();var code=p.ExitCode;
      if(code!=0&&code!=3010)return code;
      if(!Installed())throw new InvalidOperationException("No se confirmó la versión necesaria de Microsoft Visual C++.");
      log("Componente de Microsoft confirmado."+(code==3010?" Reinicio pendiente.":""));return code;
     }
    }
   }finally{try{File.Delete(file);Directory.Delete(dir,false);}catch(Exception e){log("Limpieza temporal pendiente: "+e.Message);}}
  }
 }
}
