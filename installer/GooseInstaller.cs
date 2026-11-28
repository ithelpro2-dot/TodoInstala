using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Principal;

namespace Catalogo {
 // Standalone user-scoped route. Not registered in the public installer yet.
 public static class GooseInstaller {
  public const string Url="https://github.com/aaif-goose/goose/releases/download/v1.57.0/Goose-win32-x64.zip";
  public const string Hash="931a2a539f90816e3d15027e7ec52a7c3477089723352ee6d963d9c1da48af05";
  public static bool Supports(string id,string architecture,string wingetId){return id=="goose"&&architecture=="x64"&&String.IsNullOrEmpty(wingetId);}
  public static int Install(Action<string> log){
   using(var identity=WindowsIdentity.GetCurrent())if(new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))
    throw new InvalidOperationException("Goose debe prepararse en la sesión original sin elevación.");
   if(!Environment.Is64BitOperatingSystem||Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE")=="ARM64")throw new InvalidOperationException("Esta distribución requiere Windows x64.");
   var local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
   var programs=Environment.GetFolderPath(Environment.SpecialFolder.Programs);
   if(String.IsNullOrEmpty(local)||String.IsNullOrEmpty(programs))throw new InvalidOperationException("No se pudo determinar el perfil de usuario.");
   var parent=Path.Combine(local,"Programs","TodoInstala");
   var shortcut=Path.Combine(programs,"TodoInstala - Goose.lnk");
   var existing=Path.Combine(parent,"Goose");
   if(Directory.Exists(existing)||File.Exists(existing)){log("La carpeta de Goose ya existe; se conserva.");return unchecked((int)0x8A150061);}
   if(File.Exists(shortcut)||Directory.Exists(shortcut))throw new IOException("Ya existe un acceso directo con ese nombre; se conserva.");
   Directory.CreateDirectory(parent);
   var archive=Path.Combine(parent,"goose-download-"+Guid.NewGuid().ToString("N")+".zip");
   try{
    ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
    var request=(HttpWebRequest)WebRequest.Create(Url);request.UserAgent="TodoInstala/0.1";request.Timeout=60000;request.ReadWriteTimeout=60000;
    log("Descarga oficial: "+Url);
    using(var response=InstallationWork.Receive(request)){
     if(response.ResponseUri.Scheme!="https")throw new InvalidDataException("Redirección de descarga no segura.");
     using(var input=response.GetResponseStream())using(var output=new FileStream(archive,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
      var buffer=new byte[65536];int n;long total=0;while((n=input.Read(buffer,0,buffer.Length))>0){InstallationWork.Check();total+=n;if(total>400L*1024*1024)throw new InvalidDataException("Descarga demasiado grande.");output.Write(buffer,0,n);}
     }
    }
    return InstallArchive(archive,parent,shortcut,log);
   }finally{try{File.Delete(archive);}catch(Exception e){log("Limpieza de descarga pendiente: "+e.Message);}}
  }
  // Parameters are supplied by trusted application code; never by the selection payload.
  internal static void CreateShortcut(string shortcut,string executable){
   var shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell",true));
   var link=shell.GetType().InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{shortcut});
   link.GetType().InvokeMember("TargetPath",BindingFlags.SetProperty,null,link,new object[]{executable});
   link.GetType().InvokeMember("WorkingDirectory",BindingFlags.SetProperty,null,link,new object[]{Path.GetDirectoryName(executable)});
   link.GetType().InvokeMember("Save",BindingFlags.InvokeMethod,null,link,null);
   var check=shell.GetType().InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{shortcut});
   var saved=check.GetType().InvokeMember("TargetPath",BindingFlags.GetProperty,null,check,null) as string;
   if(!String.Equals(saved,executable,StringComparison.OrdinalIgnoreCase))throw new IOException("No se pudo verificar el acceso directo.");
  }
  public static int InstallArchive(string archive,string parent,string shortcut,Action<string> log){
   var target=Path.Combine(parent,"Goose");
   if(Directory.Exists(target)||File.Exists(target)){log("La carpeta de Goose ya existe; se conserva.");return unchecked((int)0x8A150061);}
   if(File.Exists(shortcut)||Directory.Exists(shortcut))throw new IOException("Ya existe un acceso directo con ese nombre; se conserva.");
   Directory.CreateDirectory(parent);
   var staging=Path.Combine(parent,"goose-stage-"+Guid.NewGuid().ToString("N"));
   try{
    using(var file=new FileStream(archive,FileMode.Open,FileAccess.Read,FileShare.Read)){
     using(var sha=SHA256.Create()){
      var actual=BitConverter.ToString(sha.ComputeHash(file)).Replace("-","").ToLowerInvariant();
      if(actual!=Hash)throw new InvalidDataException("El archivo de Goose no coincide con la huella revisada.");
     }
     log("SHA-256 verificado: "+Hash);file.Position=0;Directory.CreateDirectory(staging);
     InstallationWork.Report(InstallationPhase.Installing);
     using(var zip=new ZipArchive(file,ZipArchiveMode.Read,true))GooseArchive.ExtractValidated(zip,staging);
    }
    var expected=Path.Combine(staging,"dist-windows","Goose.exe");
    if(!File.Exists(expected))throw new InvalidDataException("No se encontró Goose Desktop.");
    InstallationWork.Check();
    Directory.Move(staging,target);
    var executable=Path.Combine(target,"dist-windows","Goose.exe");
    CreateShortcut(shortcut,executable);
    log("Goose preparado para el usuario; no se inició.");return 0;
   }catch{
    if(Directory.Exists(staging))log("Carpeta temporal pendiente de revisión: "+staging);
    if(Directory.Exists(target))log("Copia creada pendiente de revisión: "+target);
    throw;
   }
  }
 }
}
