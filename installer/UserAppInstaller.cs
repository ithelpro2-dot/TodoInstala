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
  static string StoreArguments(string storeId){
   return "install --id "+storeId+" --exact --source msstore --silent --no-upgrade --accept-package-agreements --accept-source-agreements --disable-interactivity";
  }
  const int AlreadyInstalled=unchecked((int)0x8A150061);

  public static int Route(AppInfo app){
   if(app==null||app.architecture!="x64"||!String.IsNullOrEmpty(app.wingetId))return 0;
   if(app.id=="discord")return 8;
   if(app.id=="whatsapp")return 9;
   if(app.id=="chatgpt")return 10;
   if(app.id=="qwen-work")return 11;
   if(app.id=="openclaw")return 12;
   if(app.id=="hermes-agent")return 13;
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
    default: throw new InvalidDataException("Programa no autorizado.");
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
     using(var process=Process.Start(new ProcessStartInfo(file,"--silent"){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=dir})){
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
   using(var response=(HttpWebResponse)req.GetResponse()){
    if(response.ResponseUri.Scheme!="https")throw new InvalidDataException("Redirección insegura.");
    using(var input=response.GetResponseStream())
    using(var output=new FileStream(file,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
     var bytes=new byte[65536];int count;long total=0;
     while((count=input.Read(bytes,0,bytes.Length))>0){
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
     using(var process=Process.Start(new ProcessStartInfo(file,"/S"){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=dir})){
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
   using(var process=Process.Start(info)){
    if(process==null)throw new InvalidOperationException("No se pudo iniciar el comando requerido.");
    var error=process.StandardError.ReadToEndAsync();
    output=process.StandardOutput.ReadToEnd();
    process.WaitForExit();
    var detail=error.GetAwaiter().GetResult();
    if(!String.IsNullOrWhiteSpace(detail))output+=Environment.NewLine+detail;
    return process.ExitCode;
   }
  }
 }
}
