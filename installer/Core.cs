using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Reflection;
using System.Web.Script.Serialization;
using System.Text.RegularExpressions;

namespace Catalogo {
 public sealed class AppInfo {
  public string id {get;set;} public string name {get;set;} public string wingetId {get;set;} public string website {get;set;}
  public bool available {get;set;} public string kind {get;set;} public string architecture {get;set;}
 }
 public sealed class Catalog {public int schema {get;set;} public AppInfo[] apps {get;set;}}
 public sealed class Selection {public int schema {get;set;} public string catalogHash {get;set;} public string[] apps {get;set;}}
 public static class Core {
  public static readonly JavaScriptSerializer Json = new JavaScriptSerializer {MaxJsonLength=1048576,RecursionLimit=12};
  public static byte[] CatalogBytes() {using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("catalog.json"))using(var ms=new MemoryStream()){s.CopyTo(ms);return ms.ToArray();}}
  public static string Hash(byte[] b){using(var s=SHA256.Create())return BitConverter.ToString(s.ComputeHash(b)).Replace("-","").ToLowerInvariant();}
  public static AppInfo[] ReadSelection(string file) {
   using(var stream=File.OpenRead(file))using(var reader=new BinaryReader(stream)){
    if(stream.Length<12)throw new InvalidDataException("El instalador está incompleto.");
    stream.Seek(-12,SeekOrigin.End);uint size=reader.ReadUInt32();
    if(Encoding.ASCII.GetString(reader.ReadBytes(8))!="CATEND01"||size<1||size>4096||size>stream.Length-12)
      throw new InvalidDataException("Este archivo no incluye una selección válida. Descarga de nuevo tu instalador desde la web.");
    stream.Seek(-12-size,SeekOrigin.End);
    var selection=Json.Deserialize<Selection>(new UTF8Encoding(false,true).GetString(reader.ReadBytes((int)size)));
    var bytes=CatalogBytes();
    if(selection==null||selection.schema!=1||selection.catalogHash!=Hash(bytes))throw new InvalidDataException("El catálogo y la selección no coinciden. Descarga de nuevo el instalador.");
    var catalog=Json.Deserialize<Catalog>(Encoding.UTF8.GetString(bytes));
    if(selection.apps==null||selection.apps.Length<1||selection.apps.Length>catalog.apps.Length||selection.apps.Distinct().Count()!=selection.apps.Length)
      throw new InvalidDataException("La selección está vacía o contiene duplicados.");
    var result=new List<AppInfo>();
    foreach(var id in selection.apps) {
     var app=catalog.apps.SingleOrDefault(a=>a.id==id);
     if(!Allowed(app))throw new InvalidDataException("La selección contiene un programa no autorizado o pendiente de integrar.");
     result.Add(app);
    }
    return result.ToArray();
   }
  }
  public static AppInfo[] SelectableApps(){
   return LocalCatalog().apps.Where(Allowed).ToArray();
  }
  static Catalog LocalCatalog(){return Json.Deserialize<Catalog>(Encoding.UTF8.GetString(CatalogBytes()));}
  public static string SelectionToken(AppInfo[] selected){
   var all=LocalCatalog().apps;
   var ids=new HashSet<string>(selected.Select(app=>app.id),StringComparer.Ordinal);
   var mask=new byte[(all.Length+7)/8];
   for(int i=0;i<all.Length;i++)if(ids.Contains(all[i].id)){
    if(!Allowed(all[i]))throw new InvalidDataException("Programa no disponible.");
    mask[i/8]|=(byte)(1<<(i%8));
   }
   var body=Hash(CatalogBytes()).Substring(0,16)+"-"+BitConverter.ToString(mask).Replace("-","").ToLowerInvariant();
   return body+"-"+Hash(Encoding.ASCII.GetBytes(body)).Substring(0,12);
  }
  public static AppInfo[] DecodeFilename(string filename){
   var match=Regex.Match(Path.GetFileName(filename),@"\ATodoInstala-v1-([a-f0-9-]+)(?: \([1-9][0-9]{0,8}\))?\.exe\z",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
   return match.Success?DecodeToken(match.Groups[1].Value.ToLowerInvariant()):null;
  }
  public static AppInfo[] DecodeToken(string token){
   if(String.IsNullOrEmpty(token)||token.Length>230)return null;
   var parts=token.Split('-');
   var bytes=CatalogBytes();
   var all=Json.Deserialize<Catalog>(Encoding.UTF8.GetString(bytes)).apps;
   if(parts.Length!=3||parts[0]!=Hash(bytes).Substring(0,16)||parts[1].Length!=2*((all.Length+7)/8)||
      !Regex.IsMatch(parts[1],@"\A[a-f0-9]+\z")||parts[2]!=Hash(Encoding.ASCII.GetBytes(parts[0]+"-"+parts[1])).Substring(0,12))return null;
   var selected=new List<AppInfo>();
   for(int offset=0;offset<parts[1].Length;offset+=2){
    int value=Convert.ToInt32(parts[1].Substring(offset,2),16);
    for(int bit=0;bit<8;bit++)if((value&(1<<bit))!=0){
     int index=offset*4+bit;
     if(index>=all.Length||!Allowed(all[index]))return null;
     selected.Add(all[index]);
    }
   }
   return selected.Count==0?null:selected.ToArray();
  }
  static bool Allowed(AppInfo app){
   return app!=null&&app.available&&(app.kind=="free"||app.kind=="trial")&&
    (UserAppInstaller.Route(app)!=0||SpybotInstaller.Supports(app)||DirectInstaller.Supports(app)||CdbInstaller.Supports(app)||GooseInstaller.Supports(app.id,app.architecture,app.wingetId)||DeepSeekInstaller.Supports(app.id,app.architecture,app.wingetId)||AnythingInstaller.Supports(app.id,app.architecture,app.wingetId)||GeminiInstaller.Supports(app.id,app.architecture,app.wingetId)||SpotifyInstaller.Supports(app.id,app.architecture,app.wingetId)||ClineInstaller.Supports(app.id,app.architecture,app.wingetId)||(!String.IsNullOrEmpty(app.wingetId)&&Regex.IsMatch(app.wingetId,@"\A[A-Za-z0-9][A-Za-z0-9.+_-]+\z")))&&
    (String.IsNullOrEmpty(app.architecture)||new[]{"x64","x86","arm64"}.Contains(app.architecture));
  }
  public static string InstallArguments(AppInfo app){
   var trusted=Json.Deserialize<Catalog>(Encoding.UTF8.GetString(CatalogBytes())).apps.SingleOrDefault(a=>a.id==app.id);
   if(!Allowed(trusted)||trusted.wingetId!=app.wingetId||trusted.architecture!=app.architecture)throw new InvalidDataException("Programa no autorizado.");
   if(UserAppInstaller.Route(trusted)!=0)return UserAppInstaller.Arguments(trusted);
   if(SpybotInstaller.Supports(trusted))return SpybotInstaller.Arguments(trusted);
   if(DirectInstaller.Supports(trusted))return DirectInstaller.Arguments(trusted);
   if(CdbInstaller.Supports(trusted))return CdbInstaller.Arguments(trusted);
   if(GooseInstaller.Supports(trusted.id,trusted.architecture,trusted.wingetId))return "perfil de usuario - Goose Desktop";
   if(DeepSeekInstaller.Supports(trusted.id,trusted.architecture,trusted.wingetId))return "perfil de usuario - DeepSeek Harness /S";
   if(AnythingInstaller.Supports(trusted.id,trusted.architecture,trusted.wingetId))return "perfil de usuario - AnythingLLM /S";
   if(GeminiInstaller.Supports(trusted.id,trusted.architecture,trusted.wingetId))return "perfil de usuario - Gemini CLI";
   if(SpotifyInstaller.Supports(trusted.id,trusted.architecture,trusted.wingetId))return "perfil de usuario - Spotify /silent /skip-app-launch";
   if(ClineInstaller.Supports(trusted.id,trusted.architecture,trusted.wingetId))return "perfil de usuario - Cline CLI";
   return "install --id "+trusted.wingetId+" --exact --source winget --silent --no-upgrade --accept-package-agreements --accept-source-agreements --disable-interactivity"+(String.IsNullOrEmpty(trusted.architecture)?"":" --architecture "+trusted.architecture);
  }
  public static string Status(int code){
   uint c=unchecked((uint)code);
   if(c==0)return "Instalado";
   if(c==0x8A150061)return "Ya instalado · conservado";
   if(c==0x8A15002B)return "Sin cambios aplicables";
   if(c==3010)return "Instalado · reinicio pendiente";
   return "No se pudo instalar (0x"+c.ToString("X8")+")";
  }
  public static bool IsSuccess(int code){uint c=unchecked((uint)code);return c==0||c==0x8A150061||c==0x8A15002B||c==3010;}
  public static bool IsOfficialStoreSource(string source){
   try{
    var data=Json.Deserialize<Dictionary<string,object>>(source);
    return (string)data["Name"]=="msstore"&&((string)data["Arg"]).TrimEnd('/')=="https://storeedgefd.dsx.mp.microsoft.com/v9.0"&&
     (string)data["Identifier"]=="StoreEdgeFD"&&(string)data["Type"]=="Microsoft.Rest";
   }catch{return false;}
  }
  public static bool IsOfficialSource(string source) {
   try{
    var d=Json.Deserialize<Dictionary<string,object>>(source);
    return d.ContainsKey("Name")&&(string)d["Name"]=="winget" && d.ContainsKey("Arg") &&
     ((string)d["Arg"]).TrimEnd('/')=="https://cdn.winget.microsoft.com/cache" &&
     d.ContainsKey("Identifier")&&(string)d["Identifier"]=="Microsoft.Winget.Source_8wekyb3d8bbwe" &&
     d.ContainsKey("Type")&&(string)d["Type"]=="Microsoft.PreIndexed.Package";
   }catch{return false;}
  }
 }
}
