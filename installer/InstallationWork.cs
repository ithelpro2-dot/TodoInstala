using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace Catalogo {
 internal enum InstallationPhase { Downloading=1, Installing=2 }
 internal sealed class InstallationWork:IDisposable {
  [ThreadStatic] static InstallationWork current;
  readonly CancellationToken cancellation;
  readonly Action<InstallationPhase> progress;
  InstallationPhase lastPhase;
  readonly List<CancellationTokenRegistration> callbacks=new List<CancellationTokenRegistration>();
  readonly List<SafeFileHandle> jobs=new List<SafeFileHandle>();

  [DllImport("kernel32.dll",SetLastError=true)]
  static extern SafeFileHandle CreateJobObject(IntPtr attributes,string name);
  [DllImport("kernel32.dll",SetLastError=true)]
  static extern bool AssignProcessToJobObject(SafeFileHandle job,IntPtr process);
  [DllImport("kernel32.dll",SetLastError=true)]
  static extern bool TerminateJobObject(SafeFileHandle job,uint code);
  [UnmanagedFunctionPointer(CallingConvention.Winapi)]
  delegate int InstallerUi(IntPtr context,uint messageType,IntPtr message);
  [DllImport("msi.dll")]
  static extern uint MsiSetInternalUI(uint level,IntPtr window);
  [DllImport("msi.dll",CharSet=CharSet.Unicode)]
  static extern IntPtr MsiSetExternalUI(InstallerUi handler,uint filter,IntPtr context);
  [DllImport("msi.dll",CharSet=CharSet.Unicode)]
  static extern uint MsiInstallProduct(string packagePath,string properties);

  InstallationWork(CancellationToken token,Action<InstallationPhase> progress){cancellation=token;this.progress=progress;}

  public static T Run<T>(CancellationToken token,Func<T> work,Action<InstallationPhase> progress=null){
   token.ThrowIfCancellationRequested();
   var previous=current;
   using(var operation=new InstallationWork(token,progress)){
    current=operation;
    try{var result=work();token.ThrowIfCancellationRequested();return result;}
    catch{token.ThrowIfCancellationRequested();throw;}
    finally{current=previous;}
   }
  }

  public static void Check(){
   if(current!=null)current.cancellation.ThrowIfCancellationRequested();
  }

  static void Publish(InstallationWork operation,InstallationPhase phase){
   if(operation==null||operation.lastPhase==phase||operation.cancellation.IsCancellationRequested)return;
   operation.lastPhase=phase;
   if(operation.progress!=null)operation.progress(phase);
  }

  public static void Report(InstallationPhase phase){Publish(current,phase);}

  public static async Task<string> ReadWinGetOutput(StreamReader reader){
   var operation=current;
   var output=new StringBuilder();
   string line;
   while((line=await reader.ReadLineAsync().ConfigureAwait(false))!=null){
    var message=line.TrimStart();
    if(message.StartsWith("Downloading",StringComparison.OrdinalIgnoreCase)||
       message.StartsWith("Descargando",StringComparison.OrdinalIgnoreCase))
     Publish(operation,InstallationPhase.Downloading);
    else if(message.StartsWith("Starting package install",StringComparison.OrdinalIgnoreCase)||
            message.StartsWith("Iniciando instalación de paquete",StringComparison.OrdinalIgnoreCase))
     Publish(operation,InstallationPhase.Installing);
    output.AppendLine(line);
   }
   return output.ToString();
  }

  public static HttpWebResponse Receive(HttpWebRequest request){
   Check();
   Report(InstallationPhase.Downloading);
   if(current!=null)current.callbacks.Add(current.cancellation.Register(request.Abort));
   return (HttpWebResponse)request.GetResponse();
  }

  public static int InstallMsi(string packagePath,string properties=""){
   Check();
   Report(InstallationPhase.Installing);
   var token=current==null?CancellationToken.None:current.cancellation;
   const uint progressMessages=(1u<<8)|(1u<<9)|(1u<<10);
   InstallerUi handler=(context,messageType,message)=>token.IsCancellationRequested?2:0;
   uint previousLevel=MsiSetInternalUI(2,IntPtr.Zero);
   try{
    MsiSetExternalUI(handler,progressMessages,IntPtr.Zero);
    return (int)MsiInstallProduct(packagePath,("REBOOT=ReallySuppress "+properties).TrimEnd());
   }finally{
    MsiSetExternalUI(null,0,IntPtr.Zero);
    MsiSetInternalUI(previousLevel,IntPtr.Zero);
    GC.KeepAlive(handler);
   }
  }

  public static Process StartInstaller(ProcessStartInfo info){
   Report(InstallationPhase.Installing);
   return Start(info);
  }

  public static Process Start(ProcessStartInfo info){
   Check();
   if(current==null)return Process.Start(info);
   var job=CreateJobObject(IntPtr.Zero,null);
   if(job.IsInvalid){var error=Marshal.GetLastWin32Error();job.Dispose();throw new Win32Exception(error);}
   current.jobs.Add(job);
   var process=Process.Start(info);
   if(process==null)return null;
   try{
    if(!process.HasExited&&!AssignProcessToJobObject(job,process.Handle)){
     int error=Marshal.GetLastWin32Error();
     if(!process.HasExited)throw new Win32Exception(error,"No se pudo preparar el proceso para permitir su cancelación.");
    }
    current.callbacks.Add(current.cancellation.Register(()=>TerminateJobObject(job,1602)));
    return process;
   }catch{
    try{if(!process.HasExited)process.Kill();}
    finally{process.Dispose();}
    throw;
   }
  }

  public void Dispose(){
   foreach(var callback in callbacks)callback.Dispose();
   foreach(var job in jobs)job.Dispose();
  }
 }
}
