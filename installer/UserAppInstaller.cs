using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;

namespace Catalogo {
 public static class UserAppInstaller {
  const string DiscordVersion="1.0.9268";
  const string DiscordUrl="https://stable.dl2.discordapp.net/distro/app/stable/win/x64/"+DiscordVersion+"/DiscordSetup.exe";
  const string DiscordHash="0ec04c6e5439bfc1032d0c133a252936833ce777bfc24eddb25e947020142315";
  const string QwenUrl="https://assets.qwenwork.cn/release/latest/qwenworkcn-setup-x64.exe";
  const string QwenCertificate="19F3EAFF506EEA055434CA18677FE3074835BF95";
  const string WhatsAppStoreId="9NKSQGP7F2NH";
  const string ChatGptStoreId="9PLM9XGG6VKS";
  const string XboxStoreId="9MV0B5HZVK9Z";
  const string MusicBeeStoreId="9P4CLT2RJ1RS";
  const string MusicBeeFamily="50072StevenMayall.MusicBee_kcr266et74avj";
  static string StoreArguments(string storeId){
   return "install --id "+storeId+" --exact --source msstore --silent --no-upgrade --accept-package-agreements --accept-source-agreements --disable-interactivity";
  }
  const int AlreadyInstalled=unchecked((int)0x8A150061);

  public static int Route(AppInfo app){
   if(app==null)return 0;
   if(app.id=="musicbee"&&String.IsNullOrEmpty(app.architecture)&&String.IsNullOrEmpty(app.wingetId))return 19;
   if(app.architecture!="x64")return 0;
   if(app.id=="comfyui"&&app.wingetId=="Comfy.ComfyUI-Desktop")return 17;
   if(!String.IsNullOrEmpty(app.wingetId))return 0;
   if(app.id=="discord")return 8;
   if(app.id=="whatsapp")return 9;
   if(app.id=="chatgpt")return 10;
   if(app.id=="qwen-work")return 11;
   if(app.id=="openclaw")return 12;
   if(app.id=="hermes-agent")return 13;
   if(app.id=="aider")return 14;
   if(app.id=="continue")return 15;
   if(app.id=="openhands")return 16;
   if(app.id=="antigravity")return 18;
   if(app.id=="xbox-app")return 20;
   if(app.id=="core-temp")return 21;
   if(app.id=="roblox-player")return 22;
   if(app.id=="minecraft")return 23;
   if(app.id=="league-of-legends")return 24;
   if(app.id=="valorant")return 25;
   if(app.id=="dota-2")return 26;
   if(app.id=="pubg-battlegrounds")return 27;
   if(app.requiresSteamLicense&&app.kind=="paid"){
    if(app.id=="terraria")return 28;
    if(app.id=="grand-theft-auto-v")return 29;
   }
   return 0;
  }
  public static string Arguments(AppInfo app){
   switch(Route(app)){
    case 8: return "perfil de usuario - Discord --silent";
    case 9: return "perfil de usuario - "+StoreArguments(WhatsAppStoreId);
    case 10: return "perfil de usuario - "+StoreArguments(ChatGptStoreId);
    case 11: return "perfil de usuario - Qwen Work /S";
    case 12: return "perfil de usuario - OpenClaw CLI";
    case 13: return "perfil de usuario - Hermes Agent -NonInteractive -SkipBrowser -SkipComputerUse";
    case 14: return "perfil de usuario - Aider 0.86.2 con uv y Python 3.12";
    case 15: return "perfil de usuario - Continue CLI 1.5.47 con Node.js aislado";
    case 16: return "perfil de usuario - OpenHands Agent Canvas 1.46.0 /S /currentuser";
    case 17: return "perfil de usuario - Comfy Desktop 1.2.2 /S /currentuser";
    case 18: return "perfil de usuario - Google Antigravity 2.35.2 /S /currentuser";
    case 19: return "perfil de usuario - "+StoreArguments(MusicBeeStoreId);
    case 20: return "perfil de usuario - "+StoreArguments(XboxStoreId);
    case 21: return "perfil de usuario - Core Temp portátil x64";
    case 22: return "perfil de usuario - Roblox Player -install -silent";
    case 23: return "perfil de usuario - asistente oficial de Minecraft Launcher";
    case 24:
    case 25: return "perfil de usuario - asistente oficial de Riot";
    case 26: return "perfil de usuario - steam://install/570";
    case 27: return "perfil de usuario - steam://install/578080";
    case 28: return "perfil de usuario - steam://install/105600";
    case 29: return "perfil de usuario - steam://install/3240220";
    default: throw new InvalidDataException("Programa no autorizado.");
   }
  }
  public static int Install(AppInfo app,Action<string> log){
   using(var identity=WindowsIdentity.GetCurrent())
    if(new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))
     throw new InvalidOperationException("La instalación requiere la sesión original sin elevación.");
   if(!Environment.Is64BitOperatingSystem||Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE")=="ARM64")
    throw new InvalidOperationException("Esta distribución requiere Windows x64.");
   switch(Route(app)){
    case 8: return InstallDiscord(log);
    case 9: return InstallStore(app.name,WhatsAppStoreId,"5319275A.WhatsAppDesktop_cv1g1gvanyjgm",log);
    case 10: return InstallStore(app.name,ChatGptStoreId,"OpenAI.Codex_2p2nqsd0c76g0",log);
    case 11: return InstallQwen(log);
    case 12: return AgentInstaller.InstallOpenClaw(log);
    case 13: return AgentInstaller.InstallHermes(log);
    case 14: return AgentInstaller.InstallAider(log);
    case 15: return AgentInstaller.InstallContinue(log);
    case 16: return InstallDesktop("OpenHands Agent Canvas","1.46.0",
     "https://github.com/OpenHands/OpenHands/releases/download/v1.46.0/OpenHands-Agent-Canvas-Setup-1.46.0.exe",
     "1287952a1bc477521f6a47dd7c5772f56fddff8bc636ed0032baebe9d349e3b2",log);
    case 17: return InstallDesktop("Comfy Desktop","1.2.2",
     "https://download.todesktop.com/241130tqe9q3y/Comfy%20Desktop%20Setup%201.2.2%20-%20Build%20261125wtbx8bz4v-x64.exe",
     "8a77914b198043ef303f663d9f84051c92bbf72f70a74fa79834a52909748fae",log);
    case 18: return InstallDesktop("Antigravity","2.35.2",
     "https://storage.googleapis.com/antigravity-public/antigravity-hub/2.35.2-4585983447007232/windows-x64/Antigravity-x64.exe",
     "1c66f68fa45e9ebc55aec5479617a9eeaa305ba8b16a1bee8327b8bad9d10258",log);
    case 19:
     if(DesktopVersion("MusicBee")!=null||DirectInstaller.RegisteredProduct("MusicBee",null,true)){
      log("MusicBee ya está presente; se conserva sin actualizar.");
      return AlreadyInstalled;
     }
     return InstallStore(app.name,MusicBeeStoreId,MusicBeeFamily,log);
    case 20: return InstallStore(app.name,XboxStoreId,"Microsoft.GamingApp_8wekyb3d8bbwe",log);
    case 21: return InstallCoreTemp(log);
    case 22: return InstallRoblox(log);
    case 23: return InstallMinecraftLauncher(log);
    case 24: return OpenRiotAssistant("League of Legends",
     "https://lol.secure.dyn.riotcdn.net/channels/public/x/installer/current/live.la1.exe",
     "d21d668eda7ed4d0ecb566648b43ff4b0ece6ac2da75bd2d6dde3dd5d21bef94",log);
    case 25: return OpenRiotAssistant("Valorant",
     "https://valorant.secure.dyn.riotcdn.net/channels/public/x/installer/current/live.live.eu.exe",
     "e0d8bb7441b1d9062e48fb9350c7be0f021aee5e0d11dbaa5473397ccdd36b65",log);
    case 26: return OpenSteamAssistant(570,log);
    case 27: return OpenSteamAssistant(578080,log);
    case 28: return OpenSteamAssistant(105600,log);
    case 29: return OpenSteamAssistant(3240220,log);
    default: throw new InvalidDataException("Programa no autorizado.");
   }
  }
  static int InstallCoreTemp(Action<string> log){
   if(DirectInstaller.RegisteredProduct("Core Temp",null,true,true))return AlreadyInstalled;
   var parent=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"TodoInstala");
   Directory.CreateDirectory(parent);
   if((File.GetAttributes(parent)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Ubicación de instalación no válida.");
   var folder=Path.Combine(parent,"Core Temp");
   var executable=Path.Combine(folder,"Core Temp.exe");
   var shortcut=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"Core Temp.lnk");
   if(File.Exists(executable)){
    if((File.GetAttributes(folder)&FileAttributes.ReparsePoint)==0&&!File.Exists(shortcut)&&!Directory.Exists(shortcut))
     GooseInstaller.CreateShortcut(shortcut,executable);
    return AlreadyInstalled;
   }
   if(Directory.Exists(folder)||File.Exists(folder)||File.Exists(shortcut)||Directory.Exists(shortcut))
    throw new InvalidOperationException("Ya existe una carpeta o acceso directo de Core Temp. Se conserva.");
   var stage=Path.Combine(parent,"CoreTemp-"+Guid.NewGuid().ToString("N"));
   Directory.CreateDirectory(stage);
   var zip=Path.Combine(stage,"CoreTemp64.zip");
   try{
    Download("https://www.alcpu.com/CoreTemp/CoreTemp64.zip",zip,8L*1024*1024,log);
    using(var file=File.OpenRead(zip)){
     if(!DirectInstaller.Verify(file,"407a5dffbcaf43098200b7f6ae7875974cb7f1891b535837055de09a1f3d154f"))
      throw new InvalidDataException("La descarga de Core Temp no coincide con la huella oficial.");
     InstallationWork.Report(InstallationPhase.Installing);
     DirectInstaller.ExtractReviewedFile(file,"Core Temp.exe",Path.Combine(stage,"Core Temp.exe"),1980432);
     DirectInstaller.ExtractReviewedFile(file,"License.txt",Path.Combine(stage,"License.txt"),6594);
    }
    File.Delete(zip);
    InstallationWork.Check();
    Directory.Move(stage,folder);
    GooseInstaller.CreateShortcut(shortcut,executable);
    log("Core Temp portable preparado en la cuenta original.");
    return 0;
   }finally{
    try{if(Directory.Exists(stage))Directory.Delete(stage,true);}
    catch(IOException e){log("Limpieza pendiente: "+e.Message);}
    catch(UnauthorizedAccessException e){log("Limpieza pendiente: "+e.Message);}
   }
  }

  static string DesktopVersion(string name){
   using(var root=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall")){
    if(root!=null)foreach(var id in root.GetSubKeyNames())using(var key=root.OpenSubKey(id)){
     if(key==null)continue;
     var display=key.GetValue("DisplayName") as string;
     var version=key.GetValue("DisplayVersion") as string;
     if(String.Equals(display,name,StringComparison.OrdinalIgnoreCase)||
        (!String.IsNullOrEmpty(version)&&String.Equals(display,name+" "+version,StringComparison.OrdinalIgnoreCase)))
      return version??"";
    }
   }
   var dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs",name);
   var exe=Path.Combine(dir,name+".exe");
   if(!File.Exists(exe)||!File.Exists(Path.Combine(dir,"Uninstall "+name+".exe")))return null;
   var info=FileVersionInfo.GetVersionInfo(exe);
   if(!String.Equals(info.ProductName,name,StringComparison.OrdinalIgnoreCase))return null;
   Version versionInfo;
   return Version.TryParse(info.ProductVersion,out versionInfo)&&versionInfo.Build>=0?versionInfo.ToString(3):info.ProductVersion;
  }
  static int InstallDesktop(string name,string version,string url,string digest,Action<string> log){
   if(DesktopVersion(name)!=null){
    log(name+" ya está presente en la cuenta original; se conserva.");
    return AlreadyInstalled;
   }
   var dir=Path.Combine(Path.GetTempPath(),"TodoInstala-Desktop-"+Guid.NewGuid().ToString("N"));
   Directory.CreateDirectory(dir);
   var setup=Path.Combine(dir,"Setup.exe");
   try{
    Download(url,setup,350L*1024*1024,log);
    using(var verified=File.OpenRead(setup)){
     if(!DirectInstaller.Verify(verified,digest))throw new InvalidDataException("La descarga no coincide con el SHA-256 revisado.");
     log("SHA-256 verificado: "+digest);
     using(var process=InstallationWork.StartInstaller(new ProcessStartInfo(setup,"/S /currentuser"){
      UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=dir
     })){
      if(process==null)throw new InvalidOperationException("No se pudo iniciar "+name+".");
      process.WaitForExit();
      if(!Core.IsSuccess(process.ExitCode))return process.ExitCode;
      if(DesktopVersion(name)!=version)throw new InvalidOperationException("Windows no registró "+name+" "+version+" en la cuenta original.");
      log(name+" "+version+" confirmado en la cuenta original.");
      return process.ExitCode;
     }
    }
   }finally{
    try{File.Delete(setup);Directory.Delete(dir,false);}catch(IOException e){log("Limpieza pendiente: "+e.Message);}
    catch(UnauthorizedAccessException e){log("Limpieza pendiente: "+e.Message);}
   }
  }
  // This result represents opening a vendor assistant that still needs the user's confirmation.
  internal const int AssistantOpened=172;
  internal const int SteamSignInRequired=173;
  internal static bool IsAssistant(AppInfo app){
   int route=Route(app);
   return route>=24&&route<=29;
  }
  internal static bool RequiresSteamLicense(AppInfo app){
   int route=Route(app);
   return route==28||route==29;
  }
  static int OpenSteamAssistant(int id,Action<string> log){
   using(var protocol=Registry.ClassesRoot.OpenSubKey(@"steam\shell\open\command"))
    if(protocol==null)throw new InvalidOperationException("Instala Steam para abrir el asistente del juego.");
   bool signedIn;
   using(var active=Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam\ActiveProcess")){
    var user=active==null?null:active.GetValue("ActiveUser");
    signedIn=user is int&&(int)user!=0;
   }
   InstallationWork.Check();
   using(var launch=Process.Start(new ProcessStartInfo("steam://install/"+id){UseShellExecute=true})){}
   log("Solicitud de instalación enviada a Steam para "+id+(signedIn?". Confirma la descarga dentro de Steam.":". Steam requiere iniciar sesión."));
   return signedIn?AssistantOpened:SteamSignInRequired;
  }
  static int OpenRiotAssistant(string name,string url,string hash,Action<string> log){
   var dir=Path.Combine(Path.GetTempPath(),"TodoInstala-Riot-"+Guid.NewGuid().ToString("N"));
   Directory.CreateDirectory(dir);
   var file=Path.Combine(dir,"InstallRiot.exe");
   try{
    Download(url,file,85L*1024*1024,log);
    using(var verified=File.OpenRead(file)){
     if(!DirectInstaller.Verify(verified,hash))throw new InvalidDataException("La descarga de "+name+" no coincide con el instalador revisado.");
     using(var process=InstallationWork.StartInstaller(new ProcessStartInfo(file,"--disable-auto-launch"){
      UseShellExecute=false,WorkingDirectory=dir
     })){
      if(process==null)throw new InvalidOperationException("No se pudo iniciar el asistente de "+name+".");
      process.WaitForExit();
      if(process.ExitCode!=0)return process.ExitCode;
     }
    }
    log("Asistente oficial de "+name+" iniciado. La descarga del juego se confirma en Riot Client.");
    return AssistantOpened;
   }finally{
    try{File.Delete(file);Directory.Delete(dir,false);}catch(Exception e){log("Limpieza temporal pendiente: "+e.Message);}
   }
  }
  static int InstallMinecraftLauncher(Action<string> log){
   const string family="Microsoft.4297127D64EC6_8wekyb3d8bbwe";
   if(StorePackagePresent(family)){log("Minecraft Launcher ya presente: se conserva sin actualizar.");return AlreadyInstalled;}
   const string url="https://launcher.mojang.com/download/MinecraftInstaller.exe?ref=mcnet";
   const string hash="0fc1ded9a9459789b76104275682e603868181a0e1928ec0681810e7c8e2bf17";
   var dir=Path.Combine(Path.GetTempPath(),"TodoInstala-Minecraft-"+Guid.NewGuid().ToString("N"));
   Directory.CreateDirectory(dir);
   var file=Path.Combine(dir,"MinecraftInstaller.exe");
   try{
    Download(url,file,64L*1024*1024,log);
    using(var verified=File.OpenRead(file)){
     if(!DirectInstaller.Verify(verified,hash))throw new InvalidDataException("La descarga de Minecraft Launcher no coincide con el instalador revisado.");
     using(var process=InstallationWork.StartInstaller(new ProcessStartInfo(file){
      UseShellExecute=false,WorkingDirectory=dir
     })){
      if(process==null)throw new InvalidOperationException("No se pudo abrir el instalador de Minecraft Launcher.");
      process.WaitForExit();
      if(process.ExitCode!=0)return process.ExitCode;
     }
     if(!StorePackagePresent(family))throw new InvalidOperationException("No se confirmó Minecraft Launcher en la cuenta original.");
     log("Minecraft Launcher confirmado en la cuenta original.");
     return 0;
    }
   }finally{
    try{File.Delete(file);Directory.Delete(dir,false);}catch(Exception e){log("Limpieza temporal pendiente: "+e.Message);}
   }
  }
  static bool RobloxPresent(Environment.SpecialFolder location){
   var versions=Path.Combine(Environment.GetFolderPath(location),"Roblox","Versions");
   if(!Directory.Exists(versions))return false;
   foreach(var dir in Directory.EnumerateDirectories(versions,"version-*"))
    if(File.Exists(Path.Combine(dir,"RobloxPlayerBeta.exe")))return true;
   return false;
  }
  static int InstallRoblox(Action<string> log){
   if(RobloxPresent(Environment.SpecialFolder.LocalApplicationData)||RobloxPresent(Environment.SpecialFolder.ProgramFilesX86)||
      RobloxPresent(Environment.SpecialFolder.ProgramFiles)){
    log("Roblox Player ya presente: se conserva sin actualizar.");
    return AlreadyInstalled;
   }
   const string url="https://setup.rbxcdn.com/version-4ea8e85481e34f46-RobloxPlayerInstaller.exe";
   const string hash="c73a11fe230a8900a2e2447874d05d29134603f549e29983c47eb906dc25fc33";
   var dir=Path.Combine(Path.GetTempPath(),"TodoInstala-Roblox-"+Guid.NewGuid().ToString("N"));
   Directory.CreateDirectory(dir);
   var file=Path.Combine(dir,"RobloxPlayerInstaller.exe");
   try{
    Download(url,file,32L*1024*1024,log);
    using(var verified=File.OpenRead(file)){
     if(!DirectInstaller.Verify(verified,hash))throw new InvalidDataException("La descarga de Roblox Player no coincide con el instalador revisado.");
     using(var process=InstallationWork.StartInstaller(new ProcessStartInfo(file,"-install -silent"){
      UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=dir
     })){
      if(process==null)throw new InvalidOperationException("No se pudo iniciar Roblox Player.");
      process.WaitForExit();
      if(process.ExitCode!=0)return process.ExitCode;
     }
     if(!RobloxPresent(Environment.SpecialFolder.LocalApplicationData))
      throw new InvalidOperationException("No se confirmó Roblox Player en la cuenta original.");
     log("Roblox Player confirmado en la cuenta original.");
     return 0;
    }
   }finally{
    try{File.Delete(file);Directory.Delete(dir,false);}catch(Exception e){log("Limpieza temporal pendiente: "+e.Message);}
   }
  }
  static int InstallDiscord(Action<string> log){
   var root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Discord");
   if(DiscordPresent(root)){log("Discord ya presente: se conserva sin actualizar.");return AlreadyInstalled;}
   var dir=Path.Combine(Path.GetTempPath(),"TodoInstala-Discord-"+Guid.NewGuid().ToString("N"));
   Directory.CreateDirectory(dir);
   var file=Path.Combine(dir,"DiscordSetup.exe");
   try{
    Download(DiscordUrl,file,250L*1024*1024,log);
    using(var verified=File.OpenRead(file)){
     if(!DirectInstaller.Verify(verified,DiscordHash))throw new InvalidDataException("La descarga no coincide con la versión revisada de Discord.");
     log("SHA-256 verificado: "+DiscordHash);
     using(var process=InstallationWork.StartInstaller(new ProcessStartInfo(file,"--silent"){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=dir})){
      if(process==null)throw new InvalidOperationException("No se pudo iniciar Discord.");
      process.WaitForExit();if(process.ExitCode!=0)return process.ExitCode;
     }
     if(!File.Exists(Path.Combine(root,"app-"+DiscordVersion,"Discord.exe")))
      throw new InvalidOperationException("No se confirmó la instalación de Discord "+DiscordVersion+".");
     log("Discord confirmado en la cuenta original.");return 0;
    }
   }finally{
    try{File.Delete(file);Directory.Delete(dir,false);}catch(Exception e){log("Limpieza temporal pendiente: "+e.Message);}
   }
  }
  internal static void Download(string url,string file,long maxBytes,Action<string> log){
   ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
   var req=(HttpWebRequest)WebRequest.Create(url);
   req.UserAgent="TodoInstala/0.1";req.Timeout=60000;req.ReadWriteTimeout=60000;
   log("Descarga oficial: "+url);
   using(var response=InstallationWork.Receive(req)){
    if(response.ResponseUri.Scheme!="https")throw new InvalidDataException("Redirección insegura.");
    using(var input=response.GetResponseStream())
    using(var output=new FileStream(file,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
     var bytes=new byte[65536];int count;long total=0;
     while((count=input.Read(bytes,0,bytes.Length))>0){InstallationWork.Check();
      total+=count;if(total>maxBytes)throw new InvalidDataException("Descarga demasiado grande.");
      output.Write(bytes,0,count);
     }
    }
   }
  }
  static string QwenVersion(){
   using(var root=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall")){
    if(root==null)return null;
    foreach(var id in root.GetSubKeyNames())using(var key=root.OpenSubKey(id)){
     if(key==null)continue;
     var name=key.GetValue("DisplayName") as string;
     if(name=="千问办公"||name=="QwenWorkCN"||name=="QwenWork (CN)")return (key.GetValue("DisplayVersion") as string)??"";
    }
   }
   return null;
  }
  static int InstallQwen(Action<string> log){
   if(QwenVersion()!=null){log("Qwen Work (China) ya presente: se conserva sin actualizar.");return AlreadyInstalled;}
   var dir=Path.Combine(Path.GetTempPath(),"TodoInstala-Qwen-"+Guid.NewGuid().ToString("N"));
   Directory.CreateDirectory(dir);
   var file=Path.Combine(dir,"qwenworkcn-setup-x64.exe");
   try{
    Download(QwenUrl,file,650L*1024*1024,log);
    using(var verified=File.OpenRead(file)){
     VerifyQwen(file,log);
     log("Firma de DingTalk y versión de Qwen Work (China) verificadas.");
     using(var process=InstallationWork.StartInstaller(new ProcessStartInfo(file,"/S"){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=dir})){
      if(process==null)throw new InvalidOperationException("No se pudo iniciar Qwen Work (China).");
      process.WaitForExit();if(process.ExitCode!=0)return process.ExitCode;
     }
    }
    if(QwenVersion()!="2.1.0")throw new InvalidOperationException("No se confirmó Qwen Work (China) 2.1.0 en la cuenta original.");
    log("Qwen Work (China) 2.1.0 confirmado en la cuenta original.");return 0;
   }finally{
    try{File.Delete(file);Directory.Delete(dir,false);}catch(Exception e){log("Limpieza temporal pendiente: "+e.Message);}
   }
  }
  static string WindowsPowerShell(){
   return Path.Combine(Environment.SystemDirectory,@"WindowsPowerShell\v1.0\powershell.exe");
  }
  static void VerifyQwen(string file,Action<string> log){
   var info=FileVersionInfo.GetVersionInfo(file);
   if(info.ProductName!="QwenWorkCN"||info.ProductVersion!="2.1.0")
    throw new InvalidDataException("La descarga no contiene la edición y versión revisadas de Qwen Work (China).");
   var check="$ErrorActionPreference='Stop'; Import-Module (Join-Path $PSHOME 'Modules\\Microsoft.PowerShell.Security\\Microsoft.PowerShell.Security.psd1'); "+
    "$signature=Get-AuthenticodeSignature -LiteralPath $env:TODOINSTALA_QWEN_FILE; "+
    "if($signature.Status -eq 'Valid' -and $signature.SignerCertificate.Thumbprint -eq '"+QwenCertificate+"'){ 'valid' }else{ $signature.Status }";
   string output;
   var code=Run(WindowsPowerShell(),"-NoProfile -NonInteractive -Command \""+check+"\"",out output,
    start=>start.EnvironmentVariables["TODOINSTALA_QWEN_FILE"]=file);
   if(code!=0||output.Trim()!="valid"){
    log(output);
    throw new InvalidDataException("La firma de DingTalk no coincide con el certificado revisado.");
   }
  }
  static bool DiscordPresent(string root){
   using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\Discord"))
    if(key!=null)return true;
   if(File.Exists(Path.Combine(root,"Discord.exe")))return true;
   if(!Directory.Exists(root))return false;
   foreach(var directory in Directory.EnumerateDirectories(root,"app-*"))
    if(File.Exists(Path.Combine(directory,"Discord.exe")))return true;
   return false;
  }
  static bool StorePackagePresent(string family){
   var name=family.Substring(0,family.IndexOf('_'));
   var check="$ErrorActionPreference='Stop'; if(Get-AppxPackage -Name '"+name+"' | Where-Object PackageFamilyName -eq '"+family+"'){ 'present' }";
   string output;int code=Run(WindowsPowerShell(),"-NoProfile -NonInteractive -Command \""+check+"\"",out output);
   if(code!=0)throw new InvalidOperationException("No se pudo consultar el paquete de Microsoft Store en la cuenta original.");
   return output.Trim()=="present";
  }
  static int InstallStore(string name,string storeId,string family,Action<string> log){
   if(StorePackagePresent(family)){log(name+" ya presente: se conserva sin actualizar.");return AlreadyInstalled;}
   var winget=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Microsoft","WindowsApps","winget.exe");
   if(!File.Exists(winget))throw new InvalidOperationException(name+" requiere el Instalador de aplicación de Microsoft (WinGet).");
   string output;int code=Run(winget,"source export --name msstore",out output);
   if(code!=0||!Core.IsOfficialStoreSource(output.Trim()))
    throw new InvalidOperationException("No se confirmó el origen oficial de Microsoft Store.");
   log("Microsoft Store: "+storeId);
   code=Run(winget,StoreArguments(storeId),out output);log(output);
   if(!Core.IsSuccess(code))return code;
   if(!StorePackagePresent(family))throw new InvalidOperationException("Microsoft Store no registró "+name+" en la cuenta original.");
   log(name+" confirmado en la cuenta original.");return code;
  }
  internal static int Run(string file,string arguments,out string output,Action<ProcessStartInfo> configure=null){
   var info=new ProcessStartInfo(file,arguments){
    UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,
    StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8
   };
   if(configure!=null)configure(info);
   using(var process=InstallationWork.Start(info)){
    if(process==null)throw new InvalidOperationException("No se pudo iniciar el comando requerido.");
    var error=process.StandardError.ReadToEndAsync();
    output=arguments.StartsWith("install ",StringComparison.Ordinal)&&Path.GetFileName(file).Equals("winget.exe",StringComparison.OrdinalIgnoreCase)?
     InstallationWork.ReadWinGetOutput(process.StandardOutput).GetAwaiter().GetResult():process.StandardOutput.ReadToEnd();
    process.WaitForExit();
    var detail=error.GetAwaiter().GetResult();
    if(!String.IsNullOrWhiteSpace(detail))output+=Environment.NewLine+detail;
    return process.ExitCode;
   }
  }
 }
}
