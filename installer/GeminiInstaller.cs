using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Web.Script.Serialization;
using System.Collections.Generic;
namespace Catalogo {
 // Experimental: prepara archivos en la cuenta original; no modifica PATH ni inicia Gemini.
 public static class GeminiInstaller {
  const string NodeUrl="https://nodejs.org/dist/v24.22.2/node-v24.22.2-win-x64.zip";
  const string NodeHash="ca1bff5d8ead13c9f2ea404a176db46bd430704d5b5113734049fa365e7799e2";
  const string GeminiUrl="https://registry.npmjs.org/@google/gemini-cli/-/gemini-cli-0.67.0.tgz";
  const string GeminiHash="lDJfCcYem6/6QZdwZFiGfOu1ZxJJ+j4wsr4HlmcYBfqUHTDXLKDv1MPUXYL2Ok1ygdfMve0F5KrW/ORNZRSHIg==";
  public static bool Supports(string id,string arch,string winget){return id=="gemini-cli"&&arch=="x64"&&String.IsNullOrEmpty(winget);}
  static void Download(string url,string path,long limit,Action<string> log){
   ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;var req=(HttpWebRequest)WebRequest.Create(url);req.Timeout=60000;req.ReadWriteTimeout=60000;req.UserAgent="TodoInstala/0.1";log("Descarga oficial: "+url);
   using(var response=(HttpWebResponse)req.GetResponse()){if(response.ResponseUri.Scheme!="https")throw new InvalidDataException("Redirección no segura.");
    using(var input=response.GetResponseStream())using(var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None)){var b=new byte[65536];int n;long total=0;while((n=input.Read(b,0,b.Length))>0){total+=n;if(total>limit)throw new InvalidDataException("Descarga demasiado grande.");output.Write(b,0,n);}}}
  }
  public static bool IsPrepared(string dir){
   if(!Directory.Exists(dir)||(File.GetAttributes(dir)&FileAttributes.ReparsePoint)!=0)return false;
   var marker=Path.Combine(dir,"prepared-owned.txt");var launcher=Path.Combine(dir,"bin","gemini.cmd");
   if(!File.Exists(marker)||File.ReadAllText(marker)!="TodoInstala Gemini prepared v1"||!File.Exists(launcher))return false;
   if(File.ReadAllText(launcher)!=GeminiLauncher.Script)return false;
   return File.Exists(Path.Combine(dir,"runtime","node-v24.22.2-win-x64","node.exe"))&&File.Exists(Path.Combine(dir,"app","node_modules","@google","gemini-cli","bundle","gemini.js"));
  }
  public static string Prepare(Action<string> log){
   using(var identity=WindowsIdentity.GetCurrent())if(new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))throw new InvalidOperationException("Usa la sesión original sin elevar.");
   if(!Environment.Is64BitOperatingSystem||Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE")=="ARM64")throw new InvalidOperationException("Se requiere Windows x64.");
   var parent=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"TodoInstala");
   Directory.CreateDirectory(parent);if((File.GetAttributes(parent)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Ubicación no válida.");
   var final=Path.Combine(parent,"GeminiCLI");
   if(Directory.Exists(final)||File.Exists(final)){
    if(!IsPrepared(final)){log("Ya existe la carpeta de Gemini; se conserva.");return null;}
    var bin=Path.Combine(final,"bin");var userPath=Environment.GetEnvironmentVariable("Path",EnvironmentVariableTarget.User);
    if(GeminiPath.Contains(userPath,bin)){log("Gemini ya está preparado y registrado; se conserva.");return null;}
    if(GeminiConflict.InPathExcept(Environment.GetEnvironmentVariable("PATH"),bin)){log("Ya existe otro comando Gemini; se conserva.");return null;}
    log("Se reanudará el registro del lanzador ya preparado.");return final;
   }
   if(GeminiConflict.InPath(Environment.GetEnvironmentVariable("PATH"))){log("Ya existe un comando Gemini; se conserva.");return null;}
   var stage=Path.Combine(parent,"GeminiCLI-preparacion-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);
   try{
    var zip=Path.Combine(stage,"node.zip");var pkg=Path.Combine(stage,"gemini.tgz");Download(NodeUrl,zip,100L*1024*1024,log);Download(GeminiUrl,pkg,150L*1024*1024,log);
    using(var f=File.OpenRead(zip))using(var sha=SHA256.Create())if(BitConverter.ToString(sha.ComputeHash(f)).Replace("-","").ToLowerInvariant()!=NodeHash)throw new InvalidDataException("Node.js no coincide con la huella oficial.");
    using(var f=File.OpenRead(pkg))using(var sha=SHA512.Create())if(Convert.ToBase64String(sha.ComputeHash(f))!=GeminiHash)throw new InvalidDataException("Gemini CLI no coincide con la huella oficial.");
    var runtime=Path.Combine(stage,"runtime");Directory.CreateDirectory(runtime);using(var f=File.OpenRead(zip))using(var z=new ZipArchive(f,ZipArchiveMode.Read))NodeArchive.ExtractValidated(z,runtime);
    var node=Path.Combine(runtime,"node-v24.22.2-win-x64","node.exe");var npm=Path.Combine(runtime,"node-v24.22.2-win-x64","node_modules","npm","bin","npm-cli.js");var app=Path.Combine(stage,"app");
    var info=new ProcessStartInfo(node,"\""+npm+"\" install --prefix \""+app+"\" --no-audit --no-fund --save-exact \""+pkg+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
    info.EnvironmentVariables["NODE_OPTIONS"]="";info.EnvironmentVariables["NODE_PATH"]="";info.EnvironmentVariables["npm_config_cache"]=Path.Combine(stage,"cache");info.EnvironmentVariables["PATH"]=Path.GetDirectoryName(node)+";"+Environment.GetEnvironmentVariable("PATH");
    using(var p=Process.Start(info)){if(p==null)throw new InvalidOperationException("No se inició npm.");var stdout=p.StandardOutput.ReadToEndAsync();var stderr=p.StandardError.ReadToEndAsync();p.WaitForExit();System.Threading.Tasks.Task.WaitAll(stdout,stderr);log(stdout.Result+Environment.NewLine+stderr.Result);if(p.ExitCode!=0)throw new InvalidOperationException("Falló la instalación de Gemini CLI.");}
    var json=File.ReadAllText(Path.Combine(app,"node_modules","@google","gemini-cli","package.json"));var data=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(json);
    if((string)data["name"]!="@google/gemini-cli"||(string)data["version"]!="0.67.0"||!File.Exists(Path.Combine(app,"node_modules","@google","gemini-cli","bundle","gemini.js")))throw new InvalidDataException("Paquete instalado no coincide.");
    var bin=Path.Combine(stage,"bin");Directory.CreateDirectory(bin);GeminiLauncher.Create(bin);
    File.WriteAllText(Path.Combine(stage,"prepared-owned.txt"),"TodoInstala Gemini prepared v1");
    Directory.Move(stage,final);log("Archivos preparados; falta registrar el lanzador en PATH.");return final;
   }catch{log("Preparación incompleta conservada para revisión: "+stage);throw;}
  }
 }
}
