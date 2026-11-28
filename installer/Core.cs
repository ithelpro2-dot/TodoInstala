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
  public string id {get;set;} public string name {get;set;} public string category {get;set;} public string wingetId {get;set;} public string website {get;set;}
  public bool available {get;set;} public string kind {get;set;} public string architecture {get;set;}
  public bool requiresSteamLicense {get;set;}
  public string requiresLauncher {get;set;}
 }
 public sealed class Catalog {public int schema {get;set;} public AppInfo[] apps {get;set;}}
 public sealed class Selection {public int schema {get;set;} public string catalogHash {get;set;} public string[] apps {get;set;}}
 public sealed class PortableSelection {
  public readonly DateTime IssuedUtc;
  public readonly AppInfo[] Apps;
  internal PortableSelection(DateTime issuedUtc,AppInfo[] apps){IssuedUtc=issuedUtc;Apps=apps;}
 }
 public static class Core {
  static readonly Regex PortableCode=new Regex(@"\ATI1\.([0-9a-z]{1,9})\.([a-f0-9]{12})\.([A-Za-z0-9_-]{1,128})\.([a-f0-9]{10})\z",RegexOptions.CultureInvariant);
  static readonly TimeSpan DownloadLifetime=TimeSpan.FromHours(22);
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
    return WithLaunchers(result,catalog.apps);
   }
  }
  public static AppInfo[] SelectableApps(){
   return LocalCatalog().apps.Where(Allowed).ToArray();
  }
  internal static AppInfo[] WithLaunchers(IEnumerable<AppInfo> selection){
   return WithLaunchers(selection,LocalCatalog().apps);
  }
  static AppInfo[] WithLaunchers(IEnumerable<AppInfo> selection,AppInfo[] catalog){
   var ordered=new List<AppInfo>();
   var seen=new HashSet<string>(StringComparer.Ordinal);
   foreach(var app in selection){
    if(!String.IsNullOrEmpty(app.requiresLauncher)){
     var id=app.requiresLauncher;
     var launcher=id=="steam"||id=="epic"?catalog.SingleOrDefault(a=>a.id==id):null;
     if(!Allowed(launcher)||!String.IsNullOrEmpty(launcher.requiresLauncher))
      throw new InvalidDataException("El lanzador necesario no está disponible en este catálogo.");
     if(seen.Add(id))ordered.Add(launcher);
    }
    if(seen.Add(app.id))ordered.Add(app);
   }
   return ordered.ToArray();
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
  public static PortableSelection DecodePortableCode(string code){
   var match=PortableCode.Match(code??"");
   if(!match.Success)throw new InvalidDataException("El código está incompleto o tiene caracteres incorrectos.");
   var bytes=CatalogBytes();
   if(Hash(Encoding.ASCII.GetBytes(code.Substring(0,code.LastIndexOf('.')))).Substring(0,10)!=match.Groups[4].Value)
    throw new InvalidDataException("El código tiene un error. Cópialo de nuevo desde TodoInstala.com.");
   if(Hash(bytes).Substring(0,12)!=match.Groups[2].Value)
    throw new InvalidDataException("Este código corresponde a otro catálogo. Descarga el instalador que lo acompaña.");
   var all=Json.Deserialize<Catalog>(Encoding.UTF8.GetString(bytes)).apps;
   var encoded=match.Groups[3].Value.Replace('-','+').Replace('_','/');
   byte[] mask;
   try{mask=Convert.FromBase64String(encoded.PadRight((encoded.Length+3)/4*4,'='));}
   catch(FormatException){throw new InvalidDataException("El código de selección tiene un formato incorrecto.");}
   var selected=DecodeMask(mask,all);
   if(selected==null)throw new InvalidDataException("El código no contiene una selección disponible.");
   long stamp=0;
   foreach(char ch in match.Groups[1].Value)stamp=stamp*36+(ch<='9'?ch-'0':ch-'a'+10);
   if(stamp>253402300799999L)throw new InvalidDataException("La fecha del código no es válida.");
   return new PortableSelection(DateTimeOffset.FromUnixTimeMilliseconds(stamp).UtcDateTime,selected);
  }
  public static bool IsCurrent(DateTime issuedUtc,DateTime utcNow){
   return issuedUtc<=utcNow&&utcNow-issuedUtc<DownloadLifetime;
  }
  public static bool IsDownloadCurrent(string filename,DateTime utcNow){
   var created=File.GetCreationTimeUtc(filename);
   var written=File.GetLastWriteTimeUtc(filename);
   return IsCurrent(created<written?created:written,utcNow);
  }
  public static AppInfo[] DecodeToken(string token){
   if(String.IsNullOrEmpty(token)||token.Length>230)return null;
   var parts=token.Split('-');
   var bytes=CatalogBytes();
   var all=Json.Deserialize<Catalog>(Encoding.UTF8.GetString(bytes)).apps;
   if(parts.Length!=3||parts[0]!=Hash(bytes).Substring(0,16)||parts[1].Length!=2*((all.Length+7)/8)||
      !Regex.IsMatch(parts[1],@"\A[a-f0-9]+\z")||parts[2]!=Hash(Encoding.ASCII.GetBytes(parts[0]+"-"+parts[1])).Substring(0,12))return null;
   var mask=new byte[parts[1].Length/2];
   for(int i=0;i<mask.Length;i++)mask[i]=Convert.ToByte(parts[1].Substring(i*2,2),16);
   return DecodeMask(mask,all);
  }
  static AppInfo[] DecodeMask(byte[] mask,AppInfo[] all){
   if(mask.Length>(all.Length+7)/8)return null;
   var selected=new List<AppInfo>();
   for(int offset=0;offset<mask.Length;offset++){
    int value=mask[offset];
    for(int bit=0;bit<8;bit++)if((value&(1<<bit))!=0){
     int index=offset*8+bit;
     if(index>=all.Length||!Allowed(all[index]))return null;
     selected.Add(all[index]);
    }
   }
   return selected.Count==0?null:WithLaunchers(selected,all);
  }
  static bool Allowed(AppInfo app){
   return app!=null&&app.available&&(app.kind=="free"||app.kind=="trial"||UserAppInstaller.RequiresSteamLicense(app))&&
    (UserAppInstaller.Route(app)!=0||DockerInstaller.Supports(app)||SpybotInstaller.Supports(app)||DirectInstaller.Supports(app)||CdbInstaller.Supports(app)||GooseInstaller.Supports(app.id,app.architecture,app.wingetId)||DeepSeekInstaller.Supports(app.id,app.architecture,app.wingetId)||AnythingInstaller.Supports(app.id,app.architecture,app.wingetId)||GeminiInstaller.Supports(app.id,app.architecture,app.wingetId)||SpotifyInstaller.Supports(app.id,app.architecture,app.wingetId)||ClineInstaller.Supports(app.id,app.architecture,app.wingetId)||(!String.IsNullOrEmpty(app.wingetId)&&Regex.IsMatch(app.wingetId,@"\A[A-Za-z0-9][A-Za-z0-9.+_-]+\z")))&&
    (String.IsNullOrEmpty(app.architecture)||new[]{"x64","x86","arm64"}.Contains(app.architecture));
  }
  public static string InstallArguments(AppInfo app){
   var trusted=Json.Deserialize<Catalog>(Encoding.UTF8.GetString(CatalogBytes())).apps.SingleOrDefault(a=>a.id==app.id);
   if(!Allowed(trusted)||trusted.wingetId!=app.wingetId||trusted.architecture!=app.architecture)throw new InvalidDataException("Programa no autorizado.");
   if(UserAppInstaller.Route(trusted)!=0)return UserAppInstaller.Arguments(trusted);
   if(DockerInstaller.Supports(trusted))return DockerInstaller.Arguments(trusted);
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
