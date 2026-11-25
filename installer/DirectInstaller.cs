using System;
using System.IO;
using System.Net;
using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Win32;

namespace Catalogo {
 // Routes are compiled into the binary. A downloaded selection only supplies IDs.
 public static class DirectInstaller {
  public const string GhostscriptUrl="https://github.com/ArtifexSoftware/ghostpdl-downloads/releases/download/gs10080/gs10080w64.exe";
  public const string GhostscriptHash="52a91b8bf09298788d7a57b9206127026c23eacd75405f0a131e26dc381dce50";
  public const string RustDeskUrl="https://github.com/rustdesk/rustdesk/releases/download/1.5.0/rustdesk-1.5.0-x86_64.msi";
  public const string RustDeskHash="624fe792ab1d76b35134dab84bff97a6bcd14043ffd090a2c6a2800f77580495";
  const string AvastUrl="https://bits.avcdn.net/productfamily_ANTIVIRUS/insttype_FREE/platform_WIN/installertype_ONLINE/build_RELEASE/cookie_mmm_ava_998_999_000_m?alt=en-us";
  const string AvastHash="f740c88061c8e4838708abe86f37cb46f45af50c78662ed8d2ff6847ad38bf8c";
  const string AvgUrl="https://bits.avcdn.net/productfamily_ANTIVIRUS/insttype_FREE/platform_WIN_AVG/installertype_ONLINE/build_RELEASE/cookie_mmm_bav_998_999_000_m:dlid_FREEGSR?alt=en-us";
  const string AvgHash="d67b8939f48e27e2e08defb3134dd0b6155a3f6483b2bd84805405fcec5d3c1d";
  const string AviraFile="avira_en_sptl1_5376a394d6dbb91a__ws-spotlight-release.exe";
  const string AviraUrl="https://package.avira.com/download/spotlight-windows-bootstrapper/"+AviraFile;
  const string AviraHash="fd2209b2a2de8e802202ea116235d5396835176de39cfc7b34c25e7a17d37265";
  public static bool Supports(AppInfo app){return app!=null&&(app.id=="ghostscript"||app.id=="rustdesk"||app.id=="avast"||app.id=="avg"||app.id=="avira")&&app.architecture=="x64"&&String.IsNullOrEmpty(app.wingetId);}
  public static string Arguments(AppInfo app){
   if(!Supports(app))throw new InvalidDataException("Descarga directa no autorizada.");
   switch(app.id){
    case "rustdesk": return "/qn /norestart INSTALLPRINTER=N";
    case "avast":
    case "avg": return "/silent";
    case "avira": return "Action=Install Silent=true AcceptEula=true LaunchUi=false";
    default: return "/S";
   }
  }
  public static bool Verify(Stream stream,string expected){
   stream.Position=0;
   using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant()==expected;
  }
  static bool Installed(bool exact){
   using(var machine=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64))
   using(var root=machine.OpenSubKey(@"SOFTWARE\GPL Ghostscript")){
    if(root==null)return false;
    foreach(var version in root.GetSubKeyNames()){
     if(exact&&version!="10.08.0")continue;
     using(var key=root.OpenSubKey(version)){
      var dll=key==null?null:key.GetValue("GS_DLL") as string;
      if(!String.IsNullOrEmpty(dll)&&File.Exists(dll))return true;
     }
    }
   }
   return false;
  }
  static bool RegisteredProduct(string displayName,string versionPrefix=null){
   foreach(var view in new[]{RegistryView.Registry64,RegistryView.Registry32})
    using(var machine=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,view))
    using(var root=machine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall")){
     if(root==null)continue;
     foreach(var name in root.GetSubKeyNames())using(var key=root.OpenSubKey(name)){
      if(key==null||!String.Equals(key.GetValue("DisplayName") as string,displayName,StringComparison.OrdinalIgnoreCase))continue;
      var version=key.GetValue("DisplayVersion") as string;
      if(versionPrefix==null||(!String.IsNullOrEmpty(version)&&version.StartsWith(versionPrefix,StringComparison.Ordinal)))return true;
     }
    }
   return false;
  }
  static bool ProductInstalled(string id,bool exact){
   switch(id){
    case "rustdesk": return RegisteredProduct("RustDesk",exact?"1.5.0.":null);
    case "avast": return RegisteredProduct("Avast Free Antivirus");
    case "avg": return RegisteredProduct("AVG AntiVirus Free");
    case "avira": return RegisteredProduct("Avira Security");
    default: return Installed(exact);
   }
  }
  public static int Install(AppInfo app,Action<string> log){
   if(!Supports(app))throw new InvalidDataException("Descarga directa no autorizada.");
   if(!Environment.Is64BitOperatingSystem||Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE")=="ARM64")
    throw new InvalidOperationException("Esta distribución requiere Windows x64.");
   bool rust=app.id=="rustdesk";
   if(ProductInstalled(app.id,false)){log(app.name+" ya presente: se conserva sin actualizar.");return unchecked((int)0x8A150061);}
   string url=GhostscriptUrl,hash=GhostscriptHash,fileName="ghostscript.exe";
   string browserProduct=null;
   switch(app.id){
    case "rustdesk": url=RustDeskUrl;hash=RustDeskHash;fileName="rustdesk.msi";break;
    case "avast": url=AvastUrl;hash=AvastHash;fileName="avast_free_antivirus_setup_online.exe";browserProduct="Avast Secure Browser";break;
    case "avg": url=AvgUrl;hash=AvgHash;fileName="avg_antivirus_free_setup.exe";browserProduct="AVG Secure Browser";break;
    case "avira": url=AviraUrl;hash=AviraHash;fileName=AviraFile;browserProduct="Avira Secure Browser";break;
   }
   bool browserPresent=browserProduct!=null&&RegisteredProduct(browserProduct);
   var directory=Path.Combine(Path.GetTempPath(),"TodoInstala-"+Guid.NewGuid().ToString("N"));
   Directory.CreateDirectory(directory);
   var file=Path.Combine(directory,fileName);
   try{
    // Download first, then reopen read-only and hold the verified file against replacement.
    using(var output=new FileStream(file,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.Read)){
     ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
     var request=(HttpWebRequest)WebRequest.Create(url);
     request.UserAgent="TodoInstala/0.1";request.Timeout=60000;request.ReadWriteTimeout=60000;
     log("Descarga oficial: "+url);
     using(var response=(HttpWebResponse)request.GetResponse()){
      if(response.ResponseUri.Scheme!="https")throw new InvalidDataException("Redirección de descarga no segura.");
      using(var input=response.GetResponseStream()){
       var buffer=new byte[65536];int count;long total=0;
       while((count=input.Read(buffer,0,buffer.Length))>0){
        total+=count;if(total>200*1024*1024)throw new InvalidDataException("Descarga mayor que el límite autorizado.");
        output.Write(buffer,0,count);
       }
      }
     }
     output.Flush();
    }
    using(var verified=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read)){
     if(!Verify(verified,hash))throw new InvalidDataException("La descarga no coincide con la huella oficial. No se ejecutó.");
     log("SHA-256 verificado: "+hash);
     log("Instalador oficial "+app.name+" "+Arguments(app));
     var executable=rust?Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"msiexec.exe"):file;
     var arguments=rust?"/i \""+file+"\" "+Arguments(app):Arguments(app);
     using(var process=Process.Start(new ProcessStartInfo(executable,arguments){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=directory})){
      if(process==null)throw new InvalidOperationException("No se pudo iniciar "+app.name+".");
      process.WaitForExit();
      var code=process.ExitCode;
      if(code!=0&&code!=3010)return code;
      if(!ProductInstalled(app.id,true))throw new InvalidOperationException("El instalador terminó, pero no se confirmó el producto esperado de "+app.name+" en Windows.");
      if(browserProduct!=null&&!browserPresent&&RegisteredProduct(browserProduct))throw new InvalidOperationException("El instalador añadió "+browserProduct+".");
      log("Instalación confirmada mediante registro de Windows.");return code;
     }
    }
   }finally{
    // Only remove this invocation's named file and empty temporary directory.
    try{File.Delete(file);Directory.Delete(directory,false);}catch(Exception e){log("Limpieza temporal pendiente: "+e.Message);}
   }
  }
 }
}
