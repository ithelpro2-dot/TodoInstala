using System;
using System.IO;
using System.IO.Pipes;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Collections.Generic;
using Microsoft.Win32.SafeHandles;

namespace Catalogo {
 // Requests are limited to user-scoped routes selected in the embedded catalog.
 public sealed class GooseBridge:IDisposable {
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe,out uint id);
  readonly NamedPipeServerStream server;
  public readonly string Name;
  public GooseBridge(){
   Name="TodoInstala-Goose-"+Guid.NewGuid().ToString("N");
   var security=new PipeSecurity();
   security.SetAccessRuleProtection(true,false);
   using(var user=WindowsIdentity.GetCurrent())security.AddAccessRule(new PipeAccessRule(user.User,PipeAccessRights.FullControl,AccessControlType.Allow));
   security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid,null),PipeAccessRights.ReadWrite|PipeAccessRights.CreateNewInstance,AccessControlType.Allow));
   server=new NamedPipeServerStream(Name,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous,4096,4096,security);
  }
  public void Serve(Process expected,Func<int> install){Serve(expected,new Dictionary<int,Func<int>>{{1,install}});}
  public void Serve(Process expected,IDictionary<int,Func<int>> routes){
   var remaining=new Dictionary<int,Func<int>>(routes);
   while(remaining.Count>0){
   var pending=server.BeginWaitForConnection(null,null);
   while(!pending.AsyncWaitHandle.WaitOne(250)){
    if(expected.HasExited)throw new IOException("El proceso de instalación terminó antes de conectar.");
   }
   server.EndWaitForConnection(pending);uint id;
   if(expected.HasExited||!GetNamedPipeClientProcessId(server.SafePipeHandle,out id)||id!=(uint)expected.Id)throw new IOException("Proceso de instalación no autorizado.");
   using(var reader=new BinaryReader(server,System.Text.Encoding.UTF8,true))using(var writer=new BinaryWriter(server,System.Text.Encoding.UTF8,true)){
    if(reader.ReadInt32()!=0x47534F31)throw new InvalidDataException("Solicitud no válida.");
    var route=reader.ReadInt32();Func<int> install;
    if(!remaining.TryGetValue(route,out install))throw new InvalidDataException("Programa no seleccionado o ya procesado.");
    remaining.Remove(route);
    int code;string message="";
    try{code=install();}catch(Exception e){code=1;message=e.Message;}
    if(message.Length>2048)message=message.Substring(0,2048);
    // BinaryWriter writes directly to the pipe; a final Flush may race a client
    // that already consumed the response and closed its end.
    writer.Write(code);writer.Write(message);
   }
   server.WaitForPipeDrain();server.Disconnect();
   }
  }
  public static int Request(string name,out string message){
   return Request(name,1,out message);
  }
  public static int Request(string name,int route,out string message){
   if(route<1||route>13)throw new InvalidDataException("Programa no autorizado.");
   if(String.IsNullOrEmpty(name)||!System.Text.RegularExpressions.Regex.IsMatch(name,@"\ATodoInstala-Goose-[a-f0-9]{32}\z"))throw new InvalidDataException("Canal no válido.");
   using(var pipe=new NamedPipeClientStream(".",name,PipeDirection.InOut,PipeOptions.None)){
    pipe.Connect(30000);
    var writer=new BinaryWriter(pipe,System.Text.Encoding.UTF8,true);
    using(var reader=new BinaryReader(pipe,System.Text.Encoding.UTF8,true)){
     writer.Write(0x47534F31);writer.Write(route);writer.Flush();int code=reader.ReadInt32();message=reader.ReadString();
     try{if(pipe.ReadByte()!=-1)throw new InvalidDataException("Respuesta inesperada del canal.");}
     catch(IOException e){if((e.HResult&0xFFFF)!=109&&(e.HResult&0xFFFF)!=232)throw;}
     return code;
    }
   }
  }
  public void Dispose(){server.Dispose();}
 }
}
