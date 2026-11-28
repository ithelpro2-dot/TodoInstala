using System;
using System.IO;
using System.Net;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Principal;
namespace Catalogo {
 // Experimental: prepara Cline en la cuenta original; no registra PATH ni inicia el agente.
 public static class ClineInstaller {
  public const string Url="https://registry.npmjs.org/@cline/cli-windows-x64/-/cli-windows-x64-3.0.74.tgz";
  public const string ArchiveHash="JmCN8x3rI+LVODloLhhWW1zoOy2glqRcUKojWff6PP+vQwneP9OVSWfKnOplh0X2+YMSHFR85AwE4sY7XUjfPA==";
  public const string BinaryHash="fb1b3efa020f984235d38b64476ecbc7b3052a6a2e0f28797c147c28fb16b741";
  public static bool Supports(string id,string arch,string winget){return id=="cline"&&arch=="x64"&&String.IsNullOrEmpty(winget);}
  public static bool HasCommand(string path){foreach(var d in (path??"").Split(';')){var folder=Environment.ExpandEnvironmentVariables(d.Trim().Trim('"'));if(String.IsNullOrEmpty(folder))continue;foreach(var n in new[]{"cline.exe","cline.cmd","cline.bat","cline.com"})if(File.Exists(Path.Combine(folder,n)))return true;}return false;}
  public static string Prepare(Action<string> log){
   using(var u=WindowsIdentity.GetCurrent())if(new WindowsPrincipal(u).IsInRole(WindowsBuiltInRole.Administrator))throw new InvalidOperationException("Cline requiere la sesión original sin elevar.");
   if(!Environment.Is64BitOperatingSystem||Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE")=="ARM64")throw new InvalidOperationException("Esta distribución requiere Windows x64.");
   if(HasCommand(Environment.GetEnvironmentVariable("PATH"))){log("Ya existe un comando Cline; se conserva.");return null;}
   var parent=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"TodoInstala");Directory.CreateDirectory(parent);
   if((File.GetAttributes(parent)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Ubicación no válida.");
   var final=Path.Combine(parent,"ClineCLI");if(Directory.Exists(final)||File.Exists(final)){log("La carpeta de Cline ya existe; se conserva.");return null;}
   var stage=Path.Combine(parent,"ClineCLI-preparacion-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);
   try{
    var archive=Path.Combine(stage,"cline.tgz");ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;var req=(HttpWebRequest)WebRequest.Create(Url);req.UserAgent="TodoInstala/0.1";req.Timeout=60000;req.ReadWriteTimeout=60000;log("Descarga oficial: "+Url);
    using(var response=InstallationWork.Receive(req)){if(response.ResponseUri.Scheme!="https")throw new InvalidDataException("Redirección insegura.");using(var input=response.GetResponseStream())using(var output=new FileStream(archive,FileMode.CreateNew,FileAccess.Write,FileShare.None)){var b=new byte[65536];int n;long total=0;while((n=input.Read(b,0,b.Length))>0){InstallationWork.Check();total+=n;if(total>100L*1024*1024)throw new InvalidDataException("Descarga demasiado grande.");output.Write(b,0,n);}}}
    using(var verified=new FileStream(archive,FileMode.Open,FileAccess.Read,FileShare.Read)){
     using(var sha=SHA512.Create())if(Convert.ToBase64String(sha.ComputeHash(verified))!=ArchiveHash)throw new InvalidDataException("El paquete no coincide con la huella oficial.");
     var tar=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"tar.exe");if(!File.Exists(tar))throw new InvalidOperationException("Windows no dispone del extractor necesario.");
     var bin=Path.Combine(stage,"bin");Directory.CreateDirectory(bin);var target=Path.Combine(bin,"cline.exe");
     var info=new ProcessStartInfo(tar,"-xOzf \""+archive+"\" package/bin/cline.exe"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
     using(var p=InstallationWork.StartInstaller(info)){if(p==null)throw new InvalidOperationException("No se inició el extractor.");var err=p.StandardError.ReadToEndAsync();using(var dest=new FileStream(target,FileMode.CreateNew,FileAccess.Write,FileShare.None)){var b=new byte[65536];int n;long total=0;while((n=p.StandardOutput.BaseStream.Read(b,0,b.Length))>0){InstallationWork.Check();total+=n;if(total>200L*1024*1024)throw new InvalidDataException("Ejecutable demasiado grande.");dest.Write(b,0,n);}}p.WaitForExit();if(p.ExitCode!=0)throw new InvalidDataException("Falló la extracción: "+err.Result);}
     using(var f=File.OpenRead(target))using(var sha=SHA256.Create())if(BitConverter.ToString(sha.ComputeHash(f)).Replace("-","").ToLowerInvariant()!=BinaryHash)throw new InvalidDataException("El ejecutable no coincide con la versión revisada.");
    }
    File.WriteAllText(Path.Combine(stage,"prepared-owned.txt"),"TodoInstala Cline prepared v1");InstallationWork.Check();Directory.Move(stage,final);log("Cline preparado; falta registrar el comando en PATH.");return final;
   }catch{log("Preparación incompleta conservada para revisión: "+stage);throw;}
  }
 }
}
