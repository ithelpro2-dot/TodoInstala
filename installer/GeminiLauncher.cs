using System;
using System.IO;
using System.Text;
namespace Catalogo {
 // Preparación experimental; no modifica PATH ni inicia Gemini.
 public static class GeminiLauncher {
  public const string Script="@echo off\r\n\"%~dp0..\\runtime\\node-v24.22.2-win-x64\\node.exe\" \"%~dp0..\\app\\node_modules\\@google\\gemini-cli\\bundle\\gemini.js\" %*\r\n";
  public static void Create(string bin){
   if(!Directory.Exists(bin)||(File.GetAttributes(bin)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Carpeta del lanzador no válida.");
   var target=Path.Combine(bin,"gemini.cmd");
   using(var stream=new FileStream(target,FileMode.CreateNew,FileAccess.Write,FileShare.None))
   using(var writer=new StreamWriter(stream,new UTF8Encoding(false)))writer.Write(Script);
  }
 }
}
