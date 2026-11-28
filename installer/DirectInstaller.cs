using System;
using System.IO;
using System.Net;
using System.IO.Compression;
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
  const string CccpUrl="https://web.archive.org/web/20160512113801id_/http://www.cccp-project.net/public/Combined-Community-Codec-Pack-64bit-2015-10-18.exe";
  const string CccpHash="cf469b6be9ef379f9fc4edab6679ca58ed1dd41bb7c83e7207fd385b0b511322";
  const string FileZillaUrl="https://community.chocolatey.org/api/v2/package/filezilla/3.72.1";
  const string FileZillaPackageHash="b2f058831a72a522b1230c5e2230ff4dc5cc107bd1d00bf89babf55e00a840e5";
  const string FileZillaHash="7533c93adc1c43d7facef34bd3d19cb8808f3ab9a2201e04caff2470df55a094";
  const string FileZillaSetup="FileZilla_3.72.1_win64-setup.exe";
  const string AfterburnerUrl="https://ftp.nluug.nl/pub/games/PC/guru3d/afterburner/%5BGuru3D%5D-MSIAfterburnerSetup466Build16757.zip";
  const string AfterburnerArchiveHash="348e9690a638d84e30e32e9c7c2dd7aad9fd82d71eced1942c04c4c5620b0e1e";
  const string AfterburnerHash="1e23bafa8979865a14141e0eac4b84ed8a61a399e6698af4c0878cd500e09790";
  const string AfterburnerSetup="MSIAfterburnerSetup466.exe";
  const string Python2Url="https://www.python.org/ftp/python/2.7.18/python-2.7.18.amd64.msi";
  const string Python2Hash="b74a3afa1e0bf2a6fc566a7b70d15c9bfabba3756fb077797d16fffa27800c05";
  const string Python2Features="ALLUSERS=1 ADDLOCAL=DefaultFeature,PrivateCRT,TclTk,Documentation,Tools";
  static string Python2Folder(){return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"TodoInstala","Python27");}
  const string TemurinRelease="https://github.com/adoptium/temurin8-binaries/releases/download/jdk8u472-b08/";
  const string TemurinJreHash="a469a375c2a2358dd86bdfbb8237756c3897f6c4259af7418b0bd626a259e360";
  const string TemurinJdkHash="daff0b3a7892ec99635f54554070ede99c175c157f683bc99c6d9008e81dfe4f";
  const string TemurinArgs="ADDLOCAL=FeatureMain";
  static bool IsTemurin(string id){return id=="temurin-jre8-x86"||id=="temurin-jdk8-x86";}
  static string TemurinKind(string id){return id=="temurin-jre8-x86"?"JRE":"JDK";}
  static string TemurinFolder(string id){
   return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"TodoInstala","Temurin8-"+TemurinKind(id)+"-x86");
  }
  static bool TemurinInstalled(string id,bool exact){
   var product=id=="temurin-jre8-x86"?"{F77FFD35-A9EA-4E54-B6F0-AFAEE2AA873D}":"{34E524DB-4F04-441C-B171-7EA2C03BE86E}";
   using(var machine=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry32))
   using(var root=machine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall")){
    if(root==null)return false;
    if(exact){
     using(var entry=root.OpenSubKey(product))
      return entry!=null&&(string)entry.GetValue("DisplayVersion")=="8.0.472.8"&&
       File.Exists(Path.Combine(TemurinFolder(id),"bin","java.exe"))&&
       (TemurinKind(id)=="JRE"||File.Exists(Path.Combine(TemurinFolder(id),"bin","javac.exe")));
    }
    foreach(var name in root.GetSubKeyNames())using(var entry=root.OpenSubKey(name)){
     var display=entry==null?null:entry.GetValue("DisplayName") as string;
     Version version;
     if(display!=null&&display.StartsWith("Eclipse Temurin "+TemurinKind(id)+" ",StringComparison.OrdinalIgnoreCase)&&
        Version.TryParse(entry.GetValue("DisplayVersion") as string,out version)&&version.Major==8)return true;
    }
    return false;
   }
  }
  public static bool Supports(AppInfo app){return app!=null&&String.IsNullOrEmpty(app.wingetId)&&
   (IsTemurin(app.id)?app.architecture=="x86":app.architecture=="x64"&&(app.id=="ghostscript"||app.id=="rustdesk"||app.id=="avast"||app.id=="avg"||app.id=="avira"||app.id=="cccp"||app.id=="filezilla"||app.id=="msi-afterburner"||app.id=="razer-cortex"||app.id=="python2"));}
  public static string Arguments(AppInfo app){
   if(!Supports(app))throw new InvalidDataException("Descarga directa no autorizada.");
   switch(app.id){
    case "rustdesk": return "/qn /norestart INSTALLPRINTER=N";
    case "razer-cortex": return RazerInno;
    case "cccp": return "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-";
    case "python2": return "/qn /norestart "+Python2Features;
    case "temurin-jre8-x86":
    case "temurin-jdk8-x86": return "/qn /norestart "+TemurinArgs;
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
  internal static void ExtractReviewedFile(Stream archiveFile,string entry,string destination,long expectedLength){
   archiveFile.Position=0;
   using(var archive=new ZipArchive(archiveFile,ZipArchiveMode.Read,true)){
    ZipArchiveEntry match=null;
    foreach(var item in archive.Entries)if(String.Equals(item.FullName,entry,StringComparison.Ordinal)){
     if(match!=null)throw new InvalidDataException("Entrada duplicada.");
     match=item;
    }
    if(match==null||match.Length!=expectedLength)throw new InvalidDataException("Falta el instalador esperado.");
    using(var input=match.Open())using(var output=new FileStream(destination,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
     var buffer=new byte[65536];int count;long total=0;
     while((count=input.Read(buffer,0,buffer.Length))>0){InstallationWork.Check();
      total+=count;if(total>expectedLength)throw new InvalidDataException("Instalador demasiado grande.");
      output.Write(buffer,0,count);
     }
     if(total!=expectedLength)throw new InvalidDataException("Instalador incompleto.");
    }
   }
  }
  static bool Python2Present(){
   if(File.Exists(Path.Combine(Python2Folder(),"python.exe"))||File.Exists(@"C:\Python27\python.exe"))return true;
   foreach(var hive in new[]{RegistryHive.LocalMachine,RegistryHive.CurrentUser})
    foreach(var view in new[]{RegistryView.Registry64,RegistryView.Registry32})
     using(var registry=RegistryKey.OpenBaseKey(hive,view))
     using(var key=registry.OpenSubKey(@"SOFTWARE\Python\PythonCore\2.7\InstallPath"))
      if(key!=null)return true;
   return RegisteredProduct("Python 2.7.18 (64-bit)")||RegisteredProduct("Python 2.7.18");
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
  internal static bool RegisteredProduct(string displayName,string versionPrefix=null,bool startsWithName=false,bool includeUser=false){
   foreach(var hive in includeUser?new[]{RegistryHive.LocalMachine,RegistryHive.CurrentUser}:new[]{RegistryHive.LocalMachine})
   foreach(var view in new[]{RegistryView.Registry64,RegistryView.Registry32})
    using(var machine=RegistryKey.OpenBaseKey(hive,view))
    using(var root=machine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall")){
     if(root==null)continue;
     foreach(var name in root.GetSubKeyNames())using(var key=root.OpenSubKey(name)){
      if(key==null)continue;
      var product=key.GetValue("DisplayName") as string;
      if(!String.Equals(product,displayName,StringComparison.OrdinalIgnoreCase)&&
         !(startsWithName&&product!=null&&product.StartsWith(displayName+" ",StringComparison.OrdinalIgnoreCase)))continue;
      var version=key.GetValue("DisplayVersion") as string;
      if(versionPrefix==null||(!String.IsNullOrEmpty(version)&&version.StartsWith(versionPrefix,StringComparison.Ordinal)))return true;
     }
    }
   return false;
  }
  static bool ProductInstalled(string id,bool exact){
   switch(id){
    case "temurin-jre8-x86":
    case "temurin-jdk8-x86": return TemurinInstalled(id,exact);
    case "rustdesk": return RegisteredProduct("RustDesk",exact?"1.5.0.":null);
    case "avast": return RegisteredProduct("Avast Free Antivirus");
    case "avg": return RegisteredProduct("AVG AntiVirus Free");
    case "avira": return RegisteredProduct("Avira Security");
    case "cccp": return exact?RegisteredProduct("Combined Community Codec Pack 64bit 2015-10-18","2015.10.19.0"):
     RegisteredProduct("Combined Community Codec Pack 64bit",null,true);
    case "filezilla": return exact?RegisteredProduct("FileZilla 3.72.1","3.72.1"):RegisteredProduct("FileZilla",null,true,true);
    case "razer-cortex": return RegisteredProduct("Razer Cortex");
    case "msi-afterburner": return exact?RegisteredProduct("MSI Afterburner 4.6.6","4.6.6")&&RegisteredProduct("RivaTuner Statistics Server",null,true):RegisteredProduct("MSI Afterburner",null,true);
    case "python2": return exact?RegisteredProduct("Python 2.7.18 (64-bit)","2.7.18150")&&File.Exists(Path.Combine(Python2Folder(),"python.exe")):Python2Present();
    default: return Installed(exact);
   }
  }
  static void Download(string url,string file,long limit,Action<string> log,string id){
    using(var output=new FileStream(file,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.Read)){
     ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
     var request=(HttpWebRequest)WebRequest.Create(url);
     request.UserAgent="TodoInstala/0.1";request.Timeout=60000;request.ReadWriteTimeout=60000;
     log((id=="cccp"?"Copia archivada del instalador original: ":id=="filezilla"?"Paquete con el instalador de FileZilla: ":"Descarga oficial: ")+url);
     using(var response=InstallationWork.Receive(request)){
      if(response.ResponseUri.Scheme!="https")throw new InvalidDataException("Redirección de descarga no segura.");
      using(var input=response.GetResponseStream()){
       var buffer=new byte[65536];int count;long total=0;
       while((count=input.Read(buffer,0,buffer.Length))>0){InstallationWork.Check();
        total+=count;if(total>limit)throw new InvalidDataException("Descarga mayor que el límite autorizado.");
        output.Write(buffer,0,count);
       }
      }
     }
     output.Flush();
    }
  }
  const string RazerCdn="https://manifest-assets.razersynapse.com/";
  const string RazerInno="/SP- /VERYSILENT /SUPPRESSMSGBOXES /NORESTART";
  static string RazerVersion(string service,string value){
   using(var machine=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry32))
   using(var key=machine.OpenSubKey(@"SOFTWARE\Razer\Services\"+service))
    return key==null?null:key.GetValue(value) as string;
  }
  static int RunRazerModule(string name,string asset,long size,string hash,string args,bool inno,Action<string> log){
   var folder=Path.Combine(Path.GetTempPath(),"TodoInstala-"+Guid.NewGuid().ToString("N"));
   Directory.CreateDirectory(folder);
   var file=Path.Combine(folder,asset);
   try{
    Download(RazerCdn+asset,file,size,log,"razer-cortex");
    using(var download=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read)){
     if(download.Length!=size||!Verify(download,hash))throw new InvalidDataException("La descarga de "+name+" no coincide con el paquete verificado.");
     log("Instalación de "+name);
     using(var p=InstallationWork.StartInstaller(new ProcessStartInfo(file,args){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=folder})){
      if(p==null)throw new InvalidOperationException("No se pudo iniciar "+name+".");
      p.WaitForExit();
      return inno&&(p.ExitCode==8||p.ExitCode==100)?3010:p.ExitCode;
     }
    }
   }finally{
    try{File.Delete(file);Directory.Delete(folder,false);}catch(Exception e){log("Limpieza temporal pendiente: "+e.Message);}
   }
  }
  static int InstallRazerCortex(Action<string> log){
   var central=RazerVersion("RazerCentral","ProductVersion");
   var manager=RazerVersion("GMS3","Version");
   if(central==null){
    var result=RunRazerModule("Razer Central","1778658202qKgcx3XSRazerCentral_v7.23.0.1220.exe",121821664,
     "aace29226ac0cdf023dc06ebf4cfaac54597138d42810da0cbe99b8a90dc3fd8","/silent",false,log);
    if(result!=0)return result==3010?1603:result; // Cortex is still pending if a prerequisite needs a restart.
    central=RazerVersion("RazerCentral","ProductVersion");
   }
   if(central==null)throw new InvalidOperationException("No se confirmó Razer Central.");
   if(manager==null){
    var result=RunRazerModule("Razer Game Manager","1785486685H9YLgvmuRazerGameManager_3.15.0.1200.exe",62649280,
     "352655029a1f3d9ba2425a2818475d2b82e29d16f874a852b426ac49edff3171",RazerInno,true,log);
    if(result!=0)return result==3010?1603:result;
    manager=RazerVersion("GMS3","Version");
   }
   if(manager==null)throw new InvalidOperationException("No se confirmó Razer Game Manager.");
   var folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Razer","Razer Cortex");
   var cortex=RunRazerModule("Razer Cortex","1790653897NJTeVvQCRazerCortexSetup_11.12.0.14.exe",262554168,
    "2a9609385749b82a5f04841aa37abaf843c8e52eb09736a83611470968bb24c2",RazerInno+" /DIR=\""+folder+"\"",true,log);
   if(cortex!=0&&cortex!=3010)return cortex;
   if(!RegisteredProduct("Razer Cortex","11.12.0.14")||!File.Exists(Path.Combine(folder,"RazerCortex.exe")))
    throw new InvalidOperationException("No se confirmó Razer Cortex.");
   log("Razer Cortex confirmado mediante el registro y el ejecutable.");
   return cortex;
  }
  public static int Install(AppInfo app,Action<string> log){
   if(!Supports(app))throw new InvalidDataException("Descarga directa no autorizada.");
   if(!Environment.Is64BitOperatingSystem||Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE")=="ARM64")
    throw new InvalidOperationException("Esta distribución requiere Windows x64.");
   bool rust=app.id=="rustdesk";
   if(ProductInstalled(app.id,false)){log(app.name+" ya presente: se conserva sin actualizar.");return unchecked((int)0x8A150061);}
   if(app.id=="razer-cortex")return InstallRazerCortex(log);
   if(app.id=="cccp"&&RegisteredProduct("K-Lite Codec Pack",null,true))
    throw new InvalidOperationException("K-Lite Codec Pack ya está instalado. Se conserva su configuración de filtros.");
   if(app.id=="python2"&&(Directory.Exists(Python2Folder())||File.Exists(Python2Folder())))
    throw new InvalidOperationException("La carpeta prevista para Python 2 ya existe. Se conserva su contenido.");
   if(IsTemurin(app.id)&&(Directory.Exists(TemurinFolder(app.id))||File.Exists(TemurinFolder(app.id))))
    throw new InvalidOperationException("La carpeta prevista para Temurin ya existe. Se conserva su contenido.");
   string url=GhostscriptUrl,hash=GhostscriptHash,fileName="ghostscript.exe";
   string browserProduct=null;
   string archiveEntry=null,payloadHash=null;
   long payloadLength=0;
   switch(app.id){
    case "rustdesk": url=RustDeskUrl;hash=RustDeskHash;fileName="rustdesk.msi";break;
    case "avast": url=AvastUrl;hash=AvastHash;fileName="avast_free_antivirus_setup_online.exe";browserProduct="Avast Secure Browser";break;
    case "avg": url=AvgUrl;hash=AvgHash;fileName="avg_antivirus_free_setup.exe";browserProduct="AVG Secure Browser";break;
    case "avira": url=AviraUrl;hash=AviraHash;fileName=AviraFile;browserProduct="Avira Secure Browser";break;
    case "cccp": url=CccpUrl;hash=CccpHash;fileName="cccp-x64-2015-10-18.exe";break;
    case "filezilla":
     url=FileZillaUrl;hash=FileZillaPackageHash;fileName="filezilla.nupkg";
     archiveEntry="tools/"+FileZillaSetup;payloadHash=FileZillaHash;payloadLength=12760448;break;
    case "msi-afterburner":
     url=AfterburnerUrl;hash=AfterburnerArchiveHash;fileName="afterburner.zip";
     archiveEntry=AfterburnerSetup;payloadHash=AfterburnerHash;payloadLength=42663360;break;
    case "python2": url=Python2Url;hash=Python2Hash;fileName="python-2.7.18.amd64.msi";break;
    case "temurin-jre8-x86":
    case "temurin-jdk8-x86":
     fileName="OpenJDK8U-"+TemurinKind(app.id).ToLowerInvariant()+"_x86-32_windows_hotspot_8u472b08.msi";
     url=TemurinRelease+fileName;
     hash=app.id=="temurin-jre8-x86"?TemurinJreHash:TemurinJdkHash;
     break;
   }
   bool browserPresent=browserProduct!=null&&RegisteredProduct(browserProduct);
   var directory=Path.Combine(Path.GetTempPath(),"TodoInstala-"+Guid.NewGuid().ToString("N"));
   Directory.CreateDirectory(directory);
   var file=Path.Combine(directory,fileName);
   var executable=archiveEntry==null?file:Path.Combine(directory,Path.GetFileName(archiveEntry));
   try{
    Download(url,file,200*1024*1024,log,app.id);
    using(var verified=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read)){
     if(!Verify(verified,hash))throw new InvalidDataException("La descarga no coincide con la huella oficial. No se ejecutó.");
     log("SHA-256 verificado: "+hash);
     if(archiveEntry!=null)ExtractReviewedFile(verified,archiveEntry,executable,payloadLength);
     log("Instalador oficial "+app.name+" "+Arguments(app));
     int code;
     if(rust)code=InstallationWork.InstallMsi(file,"INSTALLPRINTER=N");
     else if(app.id=="python2")code=InstallationWork.InstallMsi(file,Python2Features+" TARGETDIR=\""+Python2Folder()+"\"");
     else if(IsTemurin(app.id))code=InstallationWork.InstallMsi(file,TemurinArgs+" INSTALLDIR=\""+TemurinFolder(app.id)+"\"");
     else using(var payload=archiveEntry!=null?File.OpenRead(executable):null){
      if(payload!=null&&!Verify(payload,payloadHash))throw new InvalidDataException("La huella del instalador de "+app.name+" no coincide.");
      using(var process=InstallationWork.StartInstaller(new ProcessStartInfo(executable,Arguments(app)){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=directory})){
       if(process==null)throw new InvalidOperationException("No se pudo iniciar "+app.name+".");
       process.WaitForExit();
       code=process.ExitCode;
      }
     }
      if(code!=0&&code!=3010)return code;
      if(!ProductInstalled(app.id,true))throw new InvalidOperationException("El instalador terminó, pero no se confirmó el producto esperado de "+app.name+" en Windows.");
      if(browserProduct!=null&&!browserPresent&&RegisteredProduct(browserProduct))throw new InvalidOperationException("El instalador añadió "+browserProduct+".");
      log("Instalación confirmada mediante registro de Windows.");return code;
    }
   }finally{
    // Only remove this invocation's named file and empty temporary directory.
    try{if(executable!=file)File.Delete(executable);File.Delete(file);Directory.Delete(directory,false);}catch(Exception e){log("Limpieza temporal pendiente: "+e.Message);}
   }
  }
 }
}
