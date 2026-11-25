using System;
using System.IO;
namespace Catalogo {
 // Experimental: comprueba conflictos sin modificar el entorno del usuario.
 public static class GeminiConflict {
  public static bool HasCommand(string directory){
   if(String.IsNullOrWhiteSpace(directory))return false;
   string expanded=Environment.ExpandEnvironmentVariables(directory.Trim().Trim('"'));
   foreach(var name in new[]{"gemini.exe","gemini.cmd","gemini.bat","gemini.com"})
    if(File.Exists(Path.Combine(expanded,name)))return true;
   return false;
  }
  public static bool InPathExcept(string path,string except){
   if(String.IsNullOrEmpty(path))return false;
   foreach(var directory in path.Split(';')){var expanded=Environment.ExpandEnvironmentVariables(directory.Trim().Trim('"')).TrimEnd('\\','/');if(String.Equals(expanded,except.TrimEnd('\\','/'),StringComparison.OrdinalIgnoreCase))continue;if(HasCommand(directory))return true;}
   return false;
  }
  public static bool InPath(string path){
   if(String.IsNullOrEmpty(path))return false;
   foreach(var directory in path.Split(';'))if(HasCommand(directory))return true;
   return false;
  }
 }
}
