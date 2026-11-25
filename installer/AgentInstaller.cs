using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;

namespace Catalogo {
 public static class AgentInstaller {
  const string NodeFolder="node-v24.22.2-win-x64";
  const string NodeUrl="https://nodejs.org/dist/v24.22.2/"+NodeFolder+".zip";
  const string NodeHash="ca1bff5d8ead13c9f2ea404a176db46bd430704d5b5113734049fa365e7799e2";
  const string OpenClawVersion="2026.11.5";
  const string OpenClawUrl="https://registry.npmjs.org/openclaw/-/openclaw-"+OpenClawVersion+".tgz";
  const string OpenClawHash="mqPlsGLOe8NS85BfP1vWW9YVwFXchsV75QTkEbyR4csylORxts+fmfZp9+qbD7aPbXQIctyjRk+CPL91fDvxBA==";
  const string HermesCommit="ae2a81ccc00e9b7c06301523e5bb09608397149c";
  const string HermesUrl="https://raw.githubusercontent.com/NousResearch/hermes-agent/"+HermesCommit+"/scripts/install.ps1";
  const string HermesHash="9bd2c1402427414a2ac9a3c3f33c66d78073ba81bb2d15634c13a52e9615d2d7";
  const string AlreadyPrepared="TodoInstala OpenClaw 2026.11.5";
  const string Launcher="@echo off\r\nsetlocal\r\nset NODE_OPTIONS=\r\nset NODE_PATH=\r\n\"%~dp0..\\runtime\\"+NodeFolder+"\\node.exe\" \"%~dp0..\\app\\node_modules\\openclaw\\openclaw.mjs\" %*\r\nexit /b %errorlevel%\r\n";
  const int AlreadyInstalled=unchecked((int)0x8A150061);

  static bool HasCommand(string command){
   foreach(var entry in (Environment.GetEnvironmentVariable("PATH")??"").Split(';')){
    var dir=Environment.ExpandEnvironmentVariables(entry.Trim().Trim('"'));
    if(String.IsNullOrEmpty(dir))continue;
    foreach(var extension in new[]{".exe",".cmd",".bat"})
     if(File.Exists(Path.Combine(dir,command+extension)))return true;
   }
   return false;
  }
  static void VerifySha256(string file,string hash){
   using(var stream=File.OpenRead(file))
    if(!DirectInstaller.Verify(stream,hash))throw new InvalidDataException("El archivo no coincide con la huella revisada.");
  }
  static string Quote(string path){return "\""+path+"\"";}
  static void Run(string file,string arguments,Action<string> log,Action<ProcessStartInfo> configure=null){
   string output;
   var code=UserAppInstaller.Run(file,arguments,out output,configure);
   if(!String.IsNullOrWhiteSpace(output))log(output);
   if(code!=0)throw new InvalidOperationException("El instalador oficial terminó con código "+code+".");
  }
  public static int InstallOpenClaw(Action<string> log){
   var local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
   var parent=Path.Combine(local,"TodoInstala");
   Directory.CreateDirectory(parent);
   if((File.GetAttributes(parent)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Ubicación de instalación no válida.");
   var final=Path.Combine(parent,"OpenClaw");
   if(HasCommand("openclaw")){log("OpenClaw ya está disponible en PATH; se conserva.");return AlreadyInstalled;}
   if(Directory.Exists(final)){
    var marker=Path.Combine(final,"prepared-owned.txt");
    if((File.GetAttributes(final)&FileAttributes.ReparsePoint)!=0||!File.Exists(marker)||File.ReadAllText(marker)!=AlreadyPrepared)
     throw new InvalidOperationException("Ya existe una carpeta de OpenClaw; se conserva su contenido.");
    RegisterOpenClaw(final,log);
    return 0;
   }
   var stage=Path.Combine(parent,"OpenClaw-preparacion-"+Guid.NewGuid().ToString("N"));
   Directory.CreateDirectory(stage);
   var zip=Path.Combine(stage,"node.zip");
   var archive=Path.Combine(stage,"openclaw.tgz");
   var runtime=Path.Combine(stage,"runtime");
   var app=Path.Combine(stage,"app");
   var cache=Path.Combine(Path.GetTempPath(),"TodoInstala-OpenClaw-cache-"+Guid.NewGuid().ToString("N"));
   try{
    UserAppInstaller.Download(NodeUrl,zip,100L*1024*1024,log);
    VerifySha256(zip,NodeHash);
    UserAppInstaller.Download(OpenClawUrl,archive,350L*1024*1024,log);
    using(var file=File.OpenRead(archive))using(var sha=SHA512.Create())
     if(Convert.ToBase64String(sha.ComputeHash(file))!=OpenClawHash)
      throw new InvalidDataException("El paquete de OpenClaw no coincide con la huella oficial.");
    Directory.CreateDirectory(runtime);
    using(var file=File.OpenRead(zip))using(var z=new ZipArchive(file,ZipArchiveMode.Read))
     NodeArchive.ExtractValidated(z,runtime);
    var node=Path.Combine(runtime,NodeFolder,"node.exe");
    var npm=Path.Combine(runtime,NodeFolder,"node_modules","npm","bin","npm-cli.js");
    var userConfig=Path.Combine(stage,"user.npmrc");
    var globalConfig=Path.Combine(stage,"global.npmrc");
    File.WriteAllText(userConfig,"");File.WriteAllText(globalConfig,"");
    var args=Quote(npm)+" install --global --prefix "+Quote(app)+" --cache "+Quote(cache)+
     " --registry https://registry.npmjs.org/ --userconfig "+Quote(userConfig)+" --globalconfig "+Quote(globalConfig)+
     " --ignore-scripts --no-audit --no-fund "+Quote(archive);
    Action<ProcessStartInfo> environment=info=>{
     info.WorkingDirectory=stage;
     info.EnvironmentVariables["NODE_OPTIONS"]="";
     info.EnvironmentVariables["NODE_PATH"]="";
     info.EnvironmentVariables["OPENCLAW_NO_ONBOARD"]="1";
    };
    Run(node,args,log,environment);
    var package=Path.Combine(app,"node_modules","openclaw");
    var metadata=Core.Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(Path.Combine(package,"package.json")));
    if((string)metadata["name"]!="openclaw"||(string)metadata["version"]!=OpenClawVersion||
       !File.Exists(Path.Combine(package,"openclaw.mjs")))throw new InvalidDataException("No se confirmó el paquete oficial de OpenClaw.");
    Run(node,Quote(Path.Combine(package,"scripts","postinstall-bundled-plugins.mjs")),log,environment);
    var bin=Path.Combine(stage,"bin");
    Directory.CreateDirectory(bin);
    File.WriteAllText(Path.Combine(bin,"openclaw.cmd"),Launcher);
    File.WriteAllText(Path.Combine(stage,"prepared-owned.txt"),AlreadyPrepared);
    File.Delete(zip);File.Delete(archive);File.Delete(userConfig);File.Delete(globalConfig);
    Directory.Move(stage,final);
    RegisterOpenClaw(final,log);
    return 0;
   }catch{
    if(Directory.Exists(stage))log("Quedaron archivos de preparación en "+stage);
    throw;
   }finally{
    try{if(Directory.Exists(cache)&&(File.GetAttributes(cache)&FileAttributes.ReparsePoint)==0)Directory.Delete(cache,true);}
    catch(IOException){log("Quedó pendiente limpiar la caché temporal de npm.");}
    catch(UnauthorizedAccessException){log("Quedó pendiente limpiar la caché temporal de npm.");}
   }
  }
  static void RegisterOpenClaw(string dir,Action<string> log){
   var bin=Path.Combine(dir,"bin");
   var launcher=Path.Combine(bin,"openclaw.cmd");
   if(!File.Exists(launcher)||File.ReadAllText(launcher)!=Launcher||
      !File.Exists(Path.Combine(dir,"app","node_modules","openclaw","openclaw.mjs")))
    throw new InvalidDataException("La instalación preparada de OpenClaw está incompleta.");
   if(GeminiPath.Register(bin,"openclaw.cmd")){
    File.WriteAllText(Path.Combine(dir,"path-owned.txt"),AlreadyPrepared);
    if(!GeminiPath.Notify())log("Abre una terminal nueva para que cargue el PATH actualizado.");
   }
   log("OpenClaw "+OpenClawVersion+" confirmado. No se inició la configuración.");
  }
  public static int InstallHermes(Action<string> log){
   var local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
   var home=Path.Combine(local,"hermes");
   var launcher=Path.Combine(home,"bin","hermes.exe");
   if(HasCommand("hermes")||File.Exists(launcher)||File.Exists(Path.Combine(home,"bin","hermes.cmd"))){
    log("Hermes Agent ya está presente; se conserva.");return AlreadyInstalled;
   }
   if(Directory.Exists(home)||!String.IsNullOrEmpty(Environment.GetEnvironmentVariable("HERMES_HOME")))
    throw new InvalidOperationException("Hay una carpeta o configuración de Hermes previa; se conserva su contenido.");
   var stage=Path.Combine(Path.GetTempPath(),"TodoInstala-Hermes-"+Guid.NewGuid().ToString("N"));
   Directory.CreateDirectory(stage);
   var script=Path.Combine(stage,"install.ps1");
   try{
    UserAppInstaller.Download(HermesUrl,script,2L*1024*1024,log);
    using(var file=File.OpenRead(script)){
     if(!DirectInstaller.Verify(file,HermesHash))throw new InvalidDataException("El instalador de Hermes no coincide con la huella revisada.");
     var powershell=Path.Combine(Environment.SystemDirectory,@"WindowsPowerShell\v1.0\powershell.exe");
     Run(powershell,"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "+Quote(script)+
      " -Channel stable -Commit "+HermesCommit+" -NonInteractive -SkipBrowser -SkipComputerUse",log,
      info=>info.EnvironmentVariables["PSModulePath"]=Path.Combine(Path.GetDirectoryName(powershell),"Modules"));
    }
    if(!File.Exists(Path.Combine(home,"hermes-agent",".hermes-bootstrap-complete"))||
       !File.Exists(launcher))
     throw new InvalidDataException("No se confirmó la instalación de Hermes Agent.");
    if(!GeminiPath.Notify())log("Abre una terminal nueva para que cargue el PATH actualizado.");
    log("Hermes Agent 0.23.2 confirmado. No se inició la configuración.");
    return 0;
   }finally{
    try{File.Delete(script);Directory.Delete(stage,false);}catch(Exception e){log("Limpieza temporal pendiente: "+e.Message);}
   }
  }
 }
}
