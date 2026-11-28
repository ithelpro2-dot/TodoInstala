using System;
using System.IO;
using System.Net;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Principal;
using Microsoft.Win32;

namespace Catalogo {
 // Invoked only in the original non-elevated user session.
 public static class AnythingInstaller {
  public const string Url="https://cdn.anythingllm.com/latest/AnythingLLMDesktop.exe";
  public const string Hash="743d10ddf920087b46cd614cafd2e0e31550089405436f00cbd827c43ca4c90a";
  const string Key=@"Software\Microsoft\Windows\CurrentVersion\Uninstall\cfb2c4c6-70ef-5070-8529-dbba19c67883";
  public static bool Supports(string id,string arch,string winget){return id=="anythingllm"&&arch=="x64"&&String.IsNullOrEmpty(winget);}
  static string Folder(){return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","AnythingLLM");}
  public static bool Present(){
   using(var root=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall")){
    if(root!=null)foreach(var name in root.GetSubKeyNames())using(var key=root.OpenSubKey(name)){
     var display=key==null?null:key.GetValue("DisplayName") as string;
     if(!String.IsNullOrEmpty(display)&&display.StartsWith("AnythingLLM",StringComparison.OrdinalIgnoreCase))return true;
    }
   }
   var folder=Folder();
   if(!Directory.Exists(folder))return false;
   if((File.GetAttributes(folder)&FileAttributes.ReparsePoint)!=0)return true;
   using(var entries=Directory.EnumerateFileSystemEntries(folder).GetEnumerator())return entries.MoveNext();
  }
  static bool Confirmed(){using(var key=Registry.CurrentUser.OpenSubKey(Key))return key!=null&&(key.GetValue("DisplayVersion") as string)=="1.19.0"&&File.Exists(Path.Combine(Folder(),"AnythingLLM.exe"));}
  public static bool LogAccepted(string text,string installed,string existing){
   return text!=null&&(text.IndexOf(installed,StringComparison.OrdinalIgnoreCase)>=0||text.IndexOf(existing,StringComparison.OrdinalIgnoreCase)>=0);
  }
  static void CheckComponent(string file,string installed,string existing,string executable){
   var path=Path.Combine(Folder(),file);
   if(!File.Exists(path)||!LogAccepted(File.ReadAllText(path),installed,existing)||!File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"anythingllm-desktop","storage","engines",executable)))
    throw new InvalidOperationException("No se confirmó un componente de AnythingLLM: "+file+". Revisa los registros.");
  }
  public static bool LlamaLogAccepted(string text){
   if(String.IsNullOrEmpty(text))return false;
   return System.Text.RegularExpressions.Regex.IsMatch(text,@"(?im)^llama\.cpp (?:b11425:[^\r\n ]+ installed successfully\.|already installed \(b11425:[^\r\n)]+\) - skipping download)\s*$");
  }
  static void CheckLlama(){
   var path=Path.Combine(Folder(),"llamacpp_install.log");
   var exe=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"anythingllm-desktop","storage","engines",@"llamacpp\cpu\llama-server.exe");
   if(!File.Exists(path)||!LlamaLogAccepted(File.ReadAllText(path))||!File.Exists(exe))throw new InvalidOperationException("No se confirmó el motor llama.cpp. Revisa los registros.");
  }
  public static int Install(Action<string> log){
   using(var identity=WindowsIdentity.GetCurrent())if(new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))throw new InvalidOperationException("AnythingLLM requiere la sesión original sin elevación.");
   if(!Environment.Is64BitOperatingSystem||Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE")=="ARM64")throw new InvalidOperationException("Esta distribución requiere Windows x64.");
   if(Present()){log("AnythingLLM ya presente: se conserva sin actualizar.");return unchecked((int)0x8A150061);}
   var dir=Path.Combine(Path.GetTempPath(),"TodoInstala-Anything-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
   var file=Path.Combine(dir,"AnythingLLMDesktop.exe");
   try{
    ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
    var req=(HttpWebRequest)WebRequest.Create(Url);req.UserAgent="TodoInstala/0.1";req.Timeout=60000;req.ReadWriteTimeout=60000;
    log("Descarga oficial: "+Url);
    using(var response=InstallationWork.Receive(req)){
     if(response.ResponseUri.Scheme!="https")throw new InvalidDataException("Redirección insegura.");
     using(var input=response.GetResponseStream())using(var output=new FileStream(file,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
      var b=new byte[65536];int n;long total=0;while((n=input.Read(b,0,b.Length))>0){InstallationWork.Check();total+=n;if(total>500L*1024*1024)throw new InvalidDataException("Descarga demasiado grande.");output.Write(b,0,n);}
     }
    }
    using(var verified=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read)){
     using(var sha=SHA256.Create())if(BitConverter.ToString(sha.ComputeHash(verified)).Replace("-","").ToLowerInvariant()!=Hash)throw new InvalidDataException("La descarga no coincide con la versión revisada de AnythingLLM.");
     log("SHA-256 verificado: "+Hash);
     using(var p=InstallationWork.StartInstaller(new ProcessStartInfo(file,"/S"){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=dir})){
      if(p==null)throw new InvalidOperationException("No se pudo iniciar AnythingLLM.");p.WaitForExit();var code=p.ExitCode;if(code!=0&&code!=3010)return code;
      if(!Confirmed())throw new InvalidOperationException("No se confirmó AnythingLLM 1.19.0 en la cuenta original.");
      CheckComponent("lemond_install.log","lemond 2026.40.0 installed successfully","lemond already installed (v2026.40.0)",@"lemond\lemond.exe");
      CheckLlama();
      CheckComponent("ffmpeg_install.log","FFMPEG bootstrap successful","already bootstrapped with version 8.0",@"ffmpeg\windows-x64\ffmpeg.exe");
      CheckComponent("meeting_assistant_install.log","Meeting Assistant assets installation completed","already bootstrapped with version 0.1.0",@"meeting-assistant\windows-x64\tinyscribe.exe");
      log("Aplicación y dependencias confirmadas en la cuenta original.");
      var runtimeLog=Path.Combine(Folder(),"vc_redist_install.log");
      if(code==3010||(File.Exists(runtimeLog)&&File.ReadAllText(runtimeLog).IndexOf("exit code: 3010",StringComparison.OrdinalIgnoreCase)>=0))return 3010;
      return 0;
     }
    }
   }finally{try{File.Delete(file);Directory.Delete(dir,false);}catch(Exception e){log("Limpieza temporal pendiente: "+e.Message);}}
  }
 }
}
