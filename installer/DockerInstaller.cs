using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Win32;

namespace Catalogo {
 public static class DockerInstaller {
  const string PendingKey=@"SOFTWARE\TodoInstala\DockerPending";
  const string DockerUrl="https://desktop.docker.com/win/main/amd64/244584/Docker%20Desktop%20Installer.exe";
  const string DockerHash="2668273f82b342a058a28f9ab53ea7bddde10bb6d51307ce2ec54c2d36459744";
  internal const int Deferred=11001;
  internal const string RestartNotice="Reinicia y Vuelve a ejecutar TodoInstala para terminar con Docker Desktop";
  const string InstallArgs="install --quiet --backend=wsl-2 --no-windows-containers";

  public static bool Supports(AppInfo app){
   return app!=null&&app.id=="docker"&&app.architecture=="x64"&&String.IsNullOrEmpty(app.wingetId);
  }
  public static string Arguments(AppInfo app){
   if(!Supports(app))throw new InvalidDataException("Ruta de Docker Desktop no autorizada.");
   return InstallArgs;
  }
  static RegistryKey Machine(){
   return RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64);
  }
  internal static string ResumeIdentity(string path){
   using(var file=File.OpenRead(path))using(var sha=SHA256.Create()){
    var image=Convert.ToBase64String(sha.ComputeHash(file));
    return Core.Hash(Encoding.UTF8.GetBytes(Path.GetFullPath(path).ToUpperInvariant()+"\n"+image));
   }
  }
  static string ReadPending(string identity){
   using(var machine=Machine())using(var key=machine.OpenSubKey(PendingKey))
    return key==null?null:key.GetValue(identity) as string;
  }
  internal static bool HasPending(string path){return ReadPending(ResumeIdentity(path))!=null;}
  static void SetPending(string identity,string boot){
   using(var machine=Machine())using(var key=machine.CreateSubKey(PendingKey))
    key.SetValue(identity,boot,RegistryValueKind.String);
  }
  static void ClearPending(string identity){
   using(var machine=Machine())using(var key=machine.OpenSubKey(PendingKey,true))
    if(key!=null)key.DeleteValue(identity,false);
  }

  static string PowerShell(string command){
   string output;
   var ps=Path.Combine(Environment.SystemDirectory,@"WindowsPowerShell\v1.0\powershell.exe");
   var code=UserAppInstaller.Run(ps,"-NoProfile -NonInteractive -Command \""+command+"\"",out output);
   if(code!=0)throw new InvalidOperationException("No se pudieron comprobar los requisitos de Docker Desktop.");
   return output.Trim();
  }
  static string BootIdentity(){
   return PowerShell("$ErrorActionPreference='Stop'; (Get-CimInstance Win32_OperatingSystem).LastBootUpTime.ToUniversalTime().Ticks");
  }
  static void CheckHost(){
   if(!Environment.Is64BitOperatingSystem||Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE")=="ARM64")
    throw new InvalidOperationException("Esta edición de Docker Desktop requiere Windows x64.");
   string data=PowerShell("$ErrorActionPreference='Stop'; $s=Get-CimInstance Win32_ComputerSystem; $p=Get-CimInstance Win32_Processor; "+
    "$o=Get-CimInstance Win32_OperatingSystem; "+
    "if($o.ProductType -ne 1){'server'} elseif($o.BuildNumber -ne '19045' -and [int]$o.BuildNumber -lt 22631){'build'} "+
    "elseif(!$s.HypervisorPresent -and !($p.VirtualizationFirmwareEnabled -contains $true)){'firmware'} "+
    "elseif(!$s.HypervisorPresent -and !($p.SecondLevelAddressTranslationExtensions -contains $true)){'slat'} "+
    "elseif($s.TotalPhysicalMemory -lt 7500000000){'ram'} "+
    "elseif((Get-Service LanmanServer).StartType -ne 'Automatic'){'service'} else {'ok'}");
   switch(data){
    case "ok": return;
    case "firmware": throw new InvalidOperationException("Activa la virtualización en BIOS/UEFI para instalar Docker Desktop.");
    case "slat": throw new InvalidOperationException("Docker Desktop requiere un procesador con virtualización SLAT.");
    case "ram": throw new InvalidOperationException("Docker Desktop requiere 8 GB de memoria.");
    case "service": throw new InvalidOperationException("Docker Desktop requiere el servicio Servidor de Windows con inicio automático.");
    default: throw new InvalidOperationException("Esta versión de Windows no cumple los requisitos de Docker Desktop.");
   }
  }
  static string WslFile(){return Path.Combine(Environment.SystemDirectory,"wsl.exe");}
  internal static Version ReadWslVersion(string output){
   // wsl.exe may write UTF-16 even when the caller is reading the redirected pipe as UTF-8.
   var first=Regex.Split(output.Replace("\0",""),@"[\r\n]+");
   foreach(var line in first){
    if(String.IsNullOrWhiteSpace(line.Trim('\uFEFF','\uFFFD',' ')))continue;
    var value=Regex.Match(line,@"\b[0-9]+\.[0-9]+\.[0-9]+(?:\.[0-9]+)?\s*$");
    Version version;
    return value.Success&&Version.TryParse(value.Value.Trim(),out version)?version:null;
   }
   return null;
  }
  static Version WslVersion(){
   string output;
   return UserAppInstaller.Run(WslFile(),"--version",out output)==0?ReadWslVersion(output):null;
  }
  static string PlatformState(){
   var dism=Path.Combine(Environment.SystemDirectory,"dism.exe");
   string output;
   if(UserAppInstaller.Run(dism,"/Online /Get-FeatureInfo /FeatureName:VirtualMachinePlatform /English",out output)!=0)
    throw new InvalidOperationException("No se pudo comprobar la plataforma de máquinas virtuales.");
   var state=Regex.Match(output.Replace("\0",""),@"(?im)^\s*State\s*:\s*([a-z ]+)");
   return state.Success?state.Groups[1].Value.Trim():"";
  }
  static bool WslReady(Version version,string platform){
   if(version==null||version<new Version(2,1,5)||platform!="Enabled")return false;
   string output;
   return UserAppInstaller.Run(WslFile(),"--status",out output)==0;
  }
  static bool NeedsWindowsRestart(Version version,string platform){
   if(version==null||version<new Version(2,1,5))return false;
   if(String.Equals(platform,"Enable Pending",StringComparison.OrdinalIgnoreCase))return true;
   using(var machine=Machine())
   using(var pending=machine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending"))
    return pending!=null;
  }
  static int Defer(string identity,string boot,Action<string> log){
   SetPending(identity,boot);
   log("WSL 2 instalado · reinicio pendiente.");
   return Deferred;
  }
  static bool Present(){
   return DirectInstaller.RegisteredProduct("Docker Desktop",null,true,true)||
    File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Docker","Docker","Docker Desktop.exe"))||
    File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","DockerDesktop","Docker Desktop.exe"));
  }
  public static int Install(AppInfo app,string installerPath,Action<string> log){
   Arguments(app);
   using(var mutex=new Mutex(false,@"Global\TodoInstala.DockerDesktop")){
    bool locked;
    try{locked=mutex.WaitOne(0);}catch(AbandonedMutexException){locked=true;}
    if(!locked)throw new InvalidOperationException("Docker Desktop ya se está instalando en otra ventana.");
    try{return InstallCore(ResumeIdentity(installerPath),log);}
    finally{mutex.ReleaseMutex();}
   }
  }
  static int InstallCore(string identity,Action<string> log){
   if(Present()){ClearPending(identity);return unchecked((int)0x8A150061);}
   var boot=BootIdentity();
   if(!Regex.IsMatch(boot,@"\A[0-9]{15,19}\z"))
    throw new InvalidOperationException("No se pudo identificar el inicio actual de Windows.");
   if(ReadPending(identity)==boot)return Deferred;
   CheckHost();
   var version=WslVersion();
   var platform=PlatformState();
   if(!WslReady(version,platform)){
    if(NeedsWindowsRestart(version,platform))return Defer(identity,boot,log);
    string details;
    bool update=version!=null&&platform=="Enabled";
    var code=UserAppInstaller.Run(WslFile(),update?"--update --web-download":"--install --no-distribution --web-download",out details);
    log(details.Replace("\0",""));
    if(code!=0&&code!=3010)return code;
    if(code==3010)return Defer(identity,boot,log);
    platform=PlatformState();
    version=WslVersion();
    if(!WslReady(version,platform)){
     if(NeedsWindowsRestart(version,platform))return Defer(identity,boot,log);
     throw new InvalidOperationException("Windows completó la preparación, pero WSL 2 todavía no está operativo.");
    }
   }
   var dir=Path.Combine(Path.GetTempPath(),"TodoInstala-Docker-"+Guid.NewGuid().ToString("N"));
   Directory.CreateDirectory(dir);
   var setup=Path.Combine(dir,"Docker Desktop Installer.exe");
   try{
    UserAppInstaller.Download(DockerUrl,setup,2L*1024*1024*1024,log);
    using(var verified=File.OpenRead(setup)){
     if(!DirectInstaller.Verify(verified,DockerHash))throw new InvalidDataException("La descarga de Docker Desktop no coincide con la huella revisada.");
     string details;
     InstallationWork.Report(InstallationPhase.Installing);
     var code=UserAppInstaller.Run(setup,InstallArgs,out details);
     log(details);
     if(code!=0&&code!=3010)return code;
     if(!Present())throw new InvalidOperationException("Windows no registró Docker Desktop.");
     ClearPending(identity);
     return code;
    }
   }finally{
    try{File.Delete(setup);Directory.Delete(dir,false);}
    catch(IOException e){log("Limpieza pendiente: "+e.Message);}
    catch(UnauthorizedAccessException e){log("Limpieza pendiente: "+e.Message);}
   }
  }
 }
}

