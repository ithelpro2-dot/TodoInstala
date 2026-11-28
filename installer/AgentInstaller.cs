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
   InstallationWork.Report(InstallationPhase.Installing);
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
    InstallationWork.Check();
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
  const string ContinueVersion="1.5.47";
  const string ContinueUrl="https://registry.npmjs.org/@continuedev/cli/-/cli-"+ContinueVersion+".tgz";
  const string ContinueHash="gtpewV3RoIOD9dyTtKIBi1SY0VOHRu3Ehe7C/mmnswm+j34MPyrcQhQaWj/m+jdfGO4fNIKdrgGIlLso1ULDFw==";
  const string AiderVersion="0.86.2";
  const string AiderWheel="aider_chat-"+AiderVersion+"-py3-none-any.whl";
  const string AiderUrl="https://files.pythonhosted.org/packages/75/f7/e20749d9a510673e7adf910b005e3efe4ceaf9c194f1dd40d6931a3f34b9/"+AiderWheel;
  const string AiderHash="64f6a0c66c9f4633ad9f479bca3e64ebcba02b9da03c6b604b74a44736b2416e";
  const string UvUrl="https://github.com/astral-sh/uv/releases/download/0.13.10/uv-x86_64-pc-windows-msvc.zip";
  const string UvHash="297b3881fe1adfb922becc2c1613202abb41dc83ea5ff43d472aa087ca1f8683";

  static string ToolRoot(){
   var root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"TodoInstala");
   Directory.CreateDirectory(root);
   if((File.GetAttributes(root)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Ubicación de instalación no válida.");
   return root;
  }
  static void CleanToolFolder(string folder,Action<string> log){
   try{if(Directory.Exists(folder)&&(File.GetAttributes(folder)&FileAttributes.ReparsePoint)==0)Directory.Delete(folder,true);}
   catch(IOException e){log("Limpieza pendiente: "+e.Message);}
   catch(UnauthorizedAccessException e){log("Limpieza pendiente: "+e.Message);}
  }
  static int RegisterTool(string dir,string command,Action<string> log,int status){
   var bin=Path.Combine(dir,"bin");
   if(GeminiPath.Register(bin,command)){
    File.WriteAllText(Path.Combine(dir,"path-owned.txt"),command);
    if(!GeminiPath.Notify())log("Abre una terminal nueva para cargar el PATH actualizado.");
   }
   return status;
  }
  static bool ExistingTool(string dir,string command,string version,Action<string> log){
   if(Directory.Exists(dir)){
    var marker=Path.Combine(dir,"prepared-owned.txt");
    if((File.GetAttributes(dir)&FileAttributes.ReparsePoint)!=0||!File.Exists(marker)||
       File.ReadAllText(marker)!=version||!File.Exists(Path.Combine(dir,"bin",command)))
     throw new InvalidOperationException("Existe una carpeta previa de la herramienta; se conserva su contenido.");
    RegisterTool(dir,command,log,AlreadyInstalled);
    return true;
   }
   return HasCommand(Path.GetFileNameWithoutExtension(command));
  }

  public static int InstallContinue(Action<string> log){
   var root=ToolRoot();
   var final=Path.Combine(root,"Continue");
   if(ExistingTool(final,"cn.cmd",ContinueVersion,log))return AlreadyInstalled;
   var stage=Path.Combine(root,"Continue-preparacion-"+Guid.NewGuid().ToString("N"));
   Directory.CreateDirectory(stage);
   try{
    var zip=Path.Combine(stage,"node.zip");
    var package=Path.Combine(stage,"continue.tgz");
    var runtime=Path.Combine(stage,"runtime");
    var prefix=Path.Combine(stage,"app");
    UserAppInstaller.Download(NodeUrl,zip,100L*1024*1024,log);
    VerifySha256(zip,NodeHash);
    Directory.CreateDirectory(runtime);
    using(var file=File.OpenRead(zip))using(var archive=new ZipArchive(file,ZipArchiveMode.Read))
     NodeArchive.ExtractValidated(archive,runtime);
    File.Delete(zip);
    UserAppInstaller.Download(ContinueUrl,package,100L*1024*1024,log);
    using(var file=File.OpenRead(package))using(var sha=SHA512.Create())
     if(Convert.ToBase64String(sha.ComputeHash(file))!=ContinueHash)throw new InvalidDataException("El paquete de Continue no coincide con la huella oficial.");
    var node=Path.Combine(runtime,NodeFolder,"node.exe");
    var npm=Path.Combine(runtime,NodeFolder,"node_modules","npm","bin","npm-cli.js");
    var userConfig=Path.Combine(stage,"user.npmrc");
    var globalConfig=Path.Combine(stage,"global.npmrc");
    File.WriteAllText(userConfig,"");File.WriteAllText(globalConfig,"");
    Action<ProcessStartInfo> clean=info=>{
     info.WorkingDirectory=stage;
     foreach(var key in new List<string>(System.Linq.Enumerable.Cast<string>(info.EnvironmentVariables.Keys)))
      if(key.StartsWith("npm_config_",StringComparison.OrdinalIgnoreCase)||key=="NODE_OPTIONS"||key=="NODE_PATH")
       info.EnvironmentVariables.Remove(key);
    };
    Run(node,Quote(npm)+" install --global --prefix "+Quote(prefix)+" --cache "+Quote(Path.Combine(stage,"cache"))+
     " --registry https://registry.npmjs.org/ --userconfig "+Quote(userConfig)+" --globalconfig "+Quote(globalConfig)+
     " --ignore-scripts --no-audit --no-fund "+Quote(package),log,clean);
    var cli=Path.Combine(prefix,"node_modules","@continuedev","cli","dist","cn.js");
    string output;
    if(!File.Exists(cli)||UserAppInstaller.Run(node,Quote(cli)+" --version",out output,clean)!=0||output.Trim()!=ContinueVersion)
     throw new InvalidDataException("No se confirmó la versión de Continue CLI.");
    var bin=Path.Combine(stage,"bin");
    Directory.CreateDirectory(bin);
    File.WriteAllText(Path.Combine(bin,"cn.cmd"),"@echo off\r\nsetlocal\r\nset NODE_OPTIONS=\r\nset NODE_PATH=\r\n\"%~dp0..\\runtime\\"+
     NodeFolder+"\\node.exe\" \"%~dp0..\\app\\node_modules\\@continuedev\\cli\\dist\\cn.js\" %*\r\nexit /b %errorlevel%\r\n");
    File.Delete(package);File.Delete(userConfig);File.Delete(globalConfig);
    CleanToolFolder(Path.Combine(stage,"cache"),log);
    File.WriteAllText(Path.Combine(stage,"prepared-owned.txt"),ContinueVersion);
    InstallationWork.Check();
    Directory.Move(stage,final);
    return RegisterTool(final,"cn.cmd",log,0);
   }finally{CleanToolFolder(stage,log);}
  }

  public static int InstallAider(Action<string> log){
   var root=ToolRoot();
   var final=Path.Combine(root,"Aider");
   if(ExistingTool(final,"aider.exe",AiderVersion,log))return AlreadyInstalled;
   // uv embeds the environment's absolute path in the launcher.
   var stage=Path.Combine(root,"Aider-preparacion-"+Guid.NewGuid().ToString("N"));
   Directory.CreateDirectory(stage);
   try{
    var zip=Path.Combine(stage,"uv.zip");
    var uv=Path.Combine(stage,"uv.exe");
    var wheel=Path.Combine(stage,AiderWheel);
    UserAppInstaller.Download(UvUrl,zip,40L*1024*1024,log);
    VerifySha256(zip,UvHash);
    using(var file=File.OpenRead(zip))using(var archive=new ZipArchive(file,ZipArchiveMode.Read)){
     var executable=archive.GetEntry("uv.exe");
     if(executable==null||executable.Length>64L*1024*1024)throw new InvalidDataException("No se encontró uv.exe en la distribución oficial.");
     using(var input=executable.Open())using(var output=File.Create(uv))input.CopyTo(output);
    }
    File.Delete(zip);
    UserAppInstaller.Download(AiderUrl,wheel,4L*1024*1024,log);
    VerifySha256(wheel,AiderHash);
    InstallationWork.Check();
    Directory.Move(stage,final);
    uv=Path.Combine(final,"uv.exe");
    wheel=Path.Combine(final,AiderWheel);
    Action<ProcessStartInfo> clean=info=>{
     info.WorkingDirectory=final;
     foreach(var key in new List<string>(System.Linq.Enumerable.Cast<string>(info.EnvironmentVariables.Keys)))
      if(key.StartsWith("UV_",StringComparison.OrdinalIgnoreCase)||key.StartsWith("PYTHON",StringComparison.OrdinalIgnoreCase)||
         key=="VIRTUAL_ENV")info.EnvironmentVariables.Remove(key);
     foreach(var binding in new[]{new[]{"UV_TOOL_DIR","tools"},new[]{"UV_TOOL_BIN_DIR","bin"},
       new[]{"UV_PYTHON_INSTALL_DIR","python"},new[]{"UV_PYTHON_BIN_DIR","python-bin"},new[]{"UV_CACHE_DIR","cache"}})
      info.EnvironmentVariables[binding[0]]=Path.Combine(final,binding[1]);
    };
    try{
     Run(uv,"--no-config --no-progress tool install --python 3.12 --managed-python --link-mode copy --no-build --default-index https://pypi.org/simple "+Quote(wheel),log,clean);
     if(!File.Exists(Path.Combine(final,"bin","aider.exe"))||!File.Exists(Path.Combine(final,"tools","aider-chat","uv-receipt.toml")))
      throw new InvalidDataException("No se confirmó la instalación de Aider.");
     File.Delete(wheel);
     CleanToolFolder(Path.Combine(final,"cache"),log);
     File.WriteAllText(Path.Combine(final,"prepared-owned.txt"),AiderVersion);
    }catch{CleanToolFolder(final,log);throw;}
    return RegisterTool(final,"aider.exe",log,0);
   }finally{CleanToolFolder(stage,log);}
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
