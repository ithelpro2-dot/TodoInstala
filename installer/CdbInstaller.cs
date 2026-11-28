using System;
using System.IO;
using System.Net;
using Microsoft.Win32;

namespace Catalogo {
 public static class CdbInstaller {
  public const string Url="https://community.chocolatey.org/api/v2/package/cdburnerxp/4.5.8.712800";
  public const string PackageHash="b14568dc17bb94e4525d75c2d7362307e8a855be37d5b0ba012ec26f17df3d95";
  public const string MsiHash="af80a5b901100d73855dd1f04845c79511cc2f1299c0ca38dfac8d03ce8fed00";
  const string Entry="tools/cdbxp_setup_x64_4.5.8.7128.msi";
  const string Product="{99A4E14B-FC7B-4CB4-B3EC-76E014558D29}";
  public static bool Supports(AppInfo a){return a!=null&&a.id=="cdburnerxp"&&a.architecture=="x64"&&String.IsNullOrEmpty(a.wingetId);}
  public static string Arguments(AppInfo a){if(!Supports(a))throw new InvalidDataException("Descarga directa no autorizada.");return "/qn /norestart";}
  static bool Installed(){
   foreach(var v in new[]{RegistryView.Registry64,RegistryView.Registry32})using(var m=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,v))
   using(var root=m.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall")){
    if(root==null)continue;
    foreach(var name in root.GetSubKeyNames())using(var k=root.OpenSubKey(name)){
     var display=k==null?null:k.GetValue("DisplayName") as string;
     if(!String.IsNullOrEmpty(display)&&display.StartsWith("CDBurnerXP",StringComparison.OrdinalIgnoreCase))return true;
    }
   }
   return false;
  }
  static bool ExactInstalled(){using(var m=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64))
   using(var k=m.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\"+Product))
    return k!=null&&(k.GetValue("DisplayVersion") as string)=="4.5.8.7128";
  }
  public static int Install(AppInfo app,Action<string> log){
   if(!Supports(app))throw new InvalidDataException("Descarga directa no autorizada.");
   if(!Environment.Is64BitOperatingSystem||Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE")=="ARM64")throw new InvalidOperationException("Esta distribución requiere Windows x64.");
   if(Installed()){log("CDBurnerXP ya presente: se conserva sin actualizar.");return unchecked((int)0x8A150061);}
   var dir=Path.Combine(Path.GetTempPath(),"TodoInstala-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
   var pkg=Path.Combine(dir,"package.zip");var msi=Path.Combine(dir,"cdb.msi");
   try{
    ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
    var request=(HttpWebRequest)WebRequest.Create(Url);request.UserAgent="TodoInstala/0.1";request.Timeout=60000;request.ReadWriteTimeout=60000;
    log("Descarga desde archivo de Chocolatey: "+Url);
    using(var response=InstallationWork.Receive(request)){
     if(response.ResponseUri.Scheme!="https")throw new InvalidDataException("Redirección de descarga no segura.");
     using(var input=response.GetResponseStream())using(var output=new FileStream(pkg,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
      var b=new byte[65536];int n;long total=0;while((n=input.Read(b,0,b.Length))>0){InstallationWork.Check();total+=n;if(total>50*1024*1024)throw new InvalidDataException("Paquete demasiado grande.");output.Write(b,0,n);}
     }
    }
    using(var verified=new FileStream(pkg,FileMode.Open,FileAccess.Read,FileShare.Read)){
     if(!DirectInstaller.Verify(verified,PackageHash))throw new InvalidDataException("El archivo descargado no coincide con la huella revisada.");
     DirectInstaller.ExtractReviewedFile(verified,Entry,msi,9220096);
    }
    using(var verified=new FileStream(msi,FileMode.Open,FileAccess.Read,FileShare.Read)){
     if(!DirectInstaller.Verify(verified,MsiHash))throw new InvalidDataException("El instalador no coincide con la huella revisada.");
     log("SHA-256 del instalador verificado: "+MsiHash);
     var code=InstallationWork.InstallMsi(msi);
     if(code!=0&&code!=3010)return code;
     if(!ExactInstalled())throw new InvalidOperationException("No se confirmó la versión esperada de CDBurnerXP.");
     log("Instalación confirmada mediante el registro de Windows.");return code;
    }
   }finally{try{File.Delete(msi);File.Delete(pkg);Directory.Delete(dir,false);}catch(Exception e){log("Limpieza temporal pendiente: "+e.Message);}}
  }
 }
}
