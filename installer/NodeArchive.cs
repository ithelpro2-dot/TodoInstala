using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;

namespace Catalogo {
 // Node.js remains unavailable until account handling and execution are integrated.
 public static class NodeArchive {
  const long MaxTotal=2L*1024*1024*1024;
  const int MaxEntries=30000;
  public static void Validate(ZipArchive archive){
   if(archive==null||archive.Entries.Count>MaxEntries)throw new InvalidDataException("Archivo de Node.js inválido o demasiado grande.");
   var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   var files=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   long total=0;bool executable=false,npm=false;
   foreach(var entry in archive.Entries){
    if(((entry.ExternalAttributes >> 16) & 0xF000)==0xA000 || (entry.ExternalAttributes & 0x400)!=0)
     throw new InvalidDataException("El archivo contiene un enlace no permitido.");
    var name=entry.FullName.Replace('\\','/');
    if(String.IsNullOrEmpty(name)||name.StartsWith("/",StringComparison.Ordinal)||name.IndexOf(':')>=0)throw new InvalidDataException("Ruta de archivo no permitida.");
    bool directory=name.EndsWith("/",StringComparison.Ordinal);
    string normalized=directory?name.Substring(0,name.Length-1):name;
    foreach(var part in normalized.Split('/')){
     if(part.Length==0||part=="."||part==".."||part.EndsWith(".",StringComparison.Ordinal)||part.EndsWith(" ",StringComparison.Ordinal)||part.IndexOfAny(Path.GetInvalidFileNameChars())>=0)
      throw new InvalidDataException("Ruta de archivo no permitida.");
     var stem=part.Split('.')[0];
     if(String.Equals(stem,"CON",StringComparison.OrdinalIgnoreCase)||String.Equals(stem,"PRN",StringComparison.OrdinalIgnoreCase)||String.Equals(stem,"AUX",StringComparison.OrdinalIgnoreCase)||String.Equals(stem,"NUL",StringComparison.OrdinalIgnoreCase)||System.Text.RegularExpressions.Regex.IsMatch(stem,@"^(COM|LPT)[1-9]$",System.Text.RegularExpressions.RegexOptions.IgnoreCase))
      throw new InvalidDataException("Nombre reservado de Windows.");
    }
    if(!normalized.StartsWith("node-v24.22.2-win-x64/",StringComparison.OrdinalIgnoreCase)&&!String.Equals(normalized,"node-v24.22.2-win-x64",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Contenido fuera de Node.js.");
    if(!paths.Add(normalized))throw new InvalidDataException("Rutas duplicadas en el archivo.");
    if(!directory)files.Add(normalized);
    if(entry.Length<0||entry.Length>MaxTotal-total)throw new InvalidDataException("Archivo de Node.js demasiado grande.");
    total+=entry.Length;
    if(String.Equals(normalized,"node-v24.22.2-win-x64/node.exe",StringComparison.Ordinal)&&!directory)executable=true;
    if(String.Equals(normalized,"node-v24.22.2-win-x64/node_modules/npm/bin/npm-cli.js",StringComparison.Ordinal)&&!directory)npm=true;
   }
   foreach(var name in paths){
    var slash=name.LastIndexOf('/');
    while(slash>0){if(files.Contains(name.Substring(0,slash)))throw new InvalidDataException("Un archivo bloquea una carpeta del paquete.");slash=name.LastIndexOf('/',slash-1);}
   }
   if(!executable||!npm)throw new InvalidDataException("No se encontraron Node.js y npm.");
  }
  // Caller must verify the archive hash and create a private empty staging directory.
  public static void ExtractValidated(ZipArchive archive,string staging){
   Validate(archive);
   if(!Directory.Exists(staging)||Directory.GetFileSystemEntries(staging).Length!=0)throw new InvalidDataException("La carpeta temporal debe estar vacía.");
   if((File.GetAttributes(staging)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Carpeta temporal no permitida.");
   var root=Path.GetFullPath(staging).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
   foreach(var entry in archive.Entries){
    var name=entry.FullName.Replace('/',Path.DirectorySeparatorChar).Replace('\\',Path.DirectorySeparatorChar);
    var output=Path.GetFullPath(Path.Combine(root,name));
    if(!output.StartsWith(root,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Ruta de extracción no permitida.");
    if(name.EndsWith(Path.DirectorySeparatorChar.ToString(),StringComparison.Ordinal)){Directory.CreateDirectory(output);continue;}
    Directory.CreateDirectory(Path.GetDirectoryName(output));
    using(var input=entry.Open())using(var dest=new FileStream(output,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
     var buffer=new byte[65536];int n;long total=0;
     while((n=input.Read(buffer,0,buffer.Length))>0){total+=n;if(total>entry.Length)throw new InvalidDataException("Contenido mayor que lo declarado.");dest.Write(buffer,0,n);}
     if(total!=entry.Length)throw new InvalidDataException("Contenido incompleto.");
    }
   }
  }
 }
}
