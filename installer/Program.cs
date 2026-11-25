using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Drawing;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.ComponentModel;

namespace Catalogo {
 static class Program {
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool AttachConsole(int pid);
  internal static bool IsAdministrator(){
   using(var identity=WindowsIdentity.GetCurrent())return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
  }
  static AppInfo[] ConfirmSelection(AppInfo[] initial){
   var choices=Core.SelectableApps();
   var selected=new HashSet<string>((initial??new AppInfo[0]).Select(app=>app.id));
   using(var form=new Form{Text="TodoInstala · Selección",StartPosition=FormStartPosition.CenterScreen,ClientSize=new Size(540,510),AutoScaleMode=AutoScaleMode.Dpi,MinimizeBox=false,MaximizeBox=false})
   using(var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(14),ColumnCount=1,RowCount=3})
   using(var heading=new Label{Dock=DockStyle.Fill,Text=initial==null?"Elige los programas que quieres instalar.":"Revisa los programas seleccionados antes de instalar."})
   using(var list=new CheckedListBox{Dock=DockStyle.Fill,CheckOnClick=true,IntegralHeight=false})
   using(var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft})
   using(var install=new Button{Text="Instalar",DialogResult=DialogResult.OK,Enabled=selected.Count>0,AutoSize=true})
   using(var cancel=new Button{Text="Cancelar",DialogResult=DialogResult.Cancel,AutoSize=true}){
    layout.RowStyles.Add(new RowStyle(SizeType.Absolute,36));
    layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
    layout.RowStyles.Add(new RowStyle(SizeType.Absolute,42));
    foreach(var app in choices)list.Items.Add(String.IsNullOrEmpty(app.architecture)?app.name:app.name+" ("+app.architecture+")",selected.Contains(app.id));
    list.ItemCheck+=(sender,e)=>install.Enabled=list.CheckedItems.Count+(e.NewValue==CheckState.Checked?1:0)-(e.CurrentValue==CheckState.Checked?1:0)>0;
    buttons.Controls.Add(install);buttons.Controls.Add(cancel);
    layout.Controls.Add(heading,0,0);layout.Controls.Add(list,0,1);layout.Controls.Add(buttons,0,2);
    form.Controls.Add(layout);form.AcceptButton=install;form.CancelButton=cancel;
    if(form.ShowDialog()!=DialogResult.OK)return null;
    return list.CheckedIndices.Cast<int>().Select(index=>choices[index]).ToArray();
   }
  }
  [STAThread] static int Main(string[] args) {
   // Inspection never launches a process, connects to the network, or installs software.
   if((args.Length==1&&args[0]=="--inspect")||(args.Length==2&&args[0]=="--inspect-name")){
    try{
     AttachConsole(-1);
     var apps=args[0]=="--inspect-name"?Core.DecodeFilename(args[1]):Core.ReadSelection(Application.ExecutablePath);
     if(apps==null)throw new InvalidDataException("La selección requiere el selector de programas.");
     Console.WriteLine(Core.Json.Serialize(new {valid=true,apps=apps.Select(a=>new {id=a.id,name=a.name,wingetId=a.wingetId,arguments=Core.InstallArguments(a)}).ToArray()}));
     return 0;
    }catch(Exception e){Console.Error.WriteLine(e.Message);return 1;}
   }
   bool bridgeRestart=args.Length==3&&args[0]=="--elevated-bridge"&&System.Text.RegularExpressions.Regex.IsMatch(args[1],@"\ATodoInstala-Goose-[a-f0-9]{32}\z");
   bool elevatedRestart=(args.Length==2&&args[0]=="--elevated"&&System.Text.RegularExpressions.Regex.IsMatch(args[1],@"\A[a-f0-9-]{30,230}\z"))||bridgeRestart;
   if(args.Length>0&&!elevatedRestart)return 2;
   Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   try{
    // Validate before requesting elevation and again in the elevated process.
    var selection=elevatedRestart?Core.DecodeToken(args[bridgeRestart?2:1]):ConfirmSelection(Core.DecodeFilename(Application.ExecutablePath));
    if(selection==null||selection.Length==0)return elevatedRestart?2:0;
    bool hasGoose=selection.Any(a=>GooseInstaller.Supports(a.id,a.architecture,a.wingetId));
    bool hasDeepSeek=selection.Any(a=>DeepSeekInstaller.Supports(a.id,a.architecture,a.wingetId));
    bool hasAnything=selection.Any(a=>AnythingInstaller.Supports(a.id,a.architecture,a.wingetId));
    bool hasGemini=selection.Any(a=>GeminiInstaller.Supports(a.id,a.architecture,a.wingetId));
    bool hasSpotify=selection.Any(a=>SpotifyInstaller.Supports(a.id,a.architecture,a.wingetId));
    bool hasCline=selection.Any(a=>ClineInstaller.Supports(a.id,a.architecture,a.wingetId));
    var userApps=selection.Where(a=>UserAppInstaller.Route(a)!=0).ToArray();
    bool hasUser=userApps.Length>0||hasGoose||hasDeepSeek||hasAnything||hasGemini||hasSpotify||hasCline;
    if(elevatedRestart&&hasUser!=bridgeRestart)throw new InvalidOperationException("No se pudo verificar la sesión original del usuario.");
    if(!IsAdministrator()){
     if(elevatedRestart)throw new InvalidOperationException("No se obtuvieron permisos de administrador. No se inició ninguna instalación.");
     try{
      using(var bridge=hasUser?new GooseBridge():null)
      using(var child=Process.Start(new ProcessStartInfo(Application.ExecutablePath,(hasUser?"--elevated-bridge "+bridge.Name:"--elevated")+" "+Core.SelectionToken(selection)){
       UseShellExecute=true,Verb="runas",WorkingDirectory=AppDomain.CurrentDomain.BaseDirectory
      })){
       if(child==null)throw new InvalidOperationException("No se pudo iniciar el instalador con permisos de administrador.");
       if(bridge!=null){
        try{
         Func<string,Action<string>> makeLog=name=>{
          var folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Catalogo","Logs");Directory.CreateDirectory(folder);
          var path=Path.Combine(folder,name+"-usuario-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6)+".log");
          return s=>File.AppendAllText(path,DateTime.Now.ToString("s")+" "+s+Environment.NewLine,Encoding.UTF8);
         };
         var routes=new Dictionary<int,Func<int>>();
         if(hasGoose)routes.Add(1,()=>GooseInstaller.Install(makeLog("goose")));
         if(hasDeepSeek)routes.Add(2,()=>DeepSeekInstaller.Install(makeLog("deepseek")));
         if(hasAnything){routes.Add(3,()=>AnythingInstaller.Present()?1:0);routes.Add(4,()=>AnythingInstaller.Install(makeLog("anythingllm")));}
         if(hasSpotify)routes.Add(6,()=>SpotifyInstaller.Install(makeLog("spotify")));
         if(hasCline)routes.Add(7,()=>{
          var log=makeLog("cline");var dir=ClineInstaller.Prepare(log);
          if(dir==null)return unchecked((int)0x8A150061);
          var bin=Path.Combine(dir,"bin");
          if(ClineInstaller.HasCommand(Environment.GetEnvironmentVariable("PATH")))throw new InvalidOperationException("Apareció otro comando Cline; no se modificó PATH.");
          bool added=GeminiPath.Register(bin,"cline.exe");
          if(added){File.WriteAllText(Path.Combine(dir,"path-owned.txt"),"TodoInstala Cline PATH v1",Encoding.UTF8);if(!GeminiPath.Notify())log("No se pudo notificar el cambio de entorno; puede ser necesario cerrar sesión.");}
          log(added?"Cline añadido al PATH del usuario; abre una nueva terminal.":"La ruta de Cline ya estaba presente.");return 0;
         });
         if(hasGemini)routes.Add(5,()=>{
          var log=makeLog("gemini");var dir=GeminiInstaller.Prepare(log);
          if(dir==null)return unchecked((int)0x8A150061);
          var bin=Path.Combine(dir,"bin");
          if(GeminiConflict.InPathExcept(Environment.GetEnvironmentVariable("PATH"),bin))throw new InvalidOperationException("Apareció otro comando Gemini; no se modificó PATH.");
          bool added=GeminiPath.Register(bin);
          if(added){File.WriteAllText(Path.Combine(dir,"path-owned.txt"),"TodoInstala Gemini PATH v1",Encoding.UTF8);if(!GeminiPath.Notify())log("No se pudo notificar el cambio de entorno; puede ser necesario cerrar sesión.");}
          log(added?"Lanzador añadido al PATH del usuario; abre una nueva terminal.":"La ruta del lanzador ya estaba presente.");return 0;
         });
         foreach(var app in userApps)routes.Add(UserAppInstaller.Route(app),()=>UserAppInstaller.Install(app,makeLog(app.id)));
         bridge.Serve(child,routes);
        }catch(IOException){if(!child.HasExited)throw;}
       }
      }
      return 0;
     }catch(Win32Exception e){
      if(e.NativeErrorCode!=1223)throw;
      MessageBox.Show("No se concedió el permiso de Windows. No se instaló ningún programa.","Catálogo · Instalación cancelada",MessageBoxButtons.OK,MessageBoxIcon.Information);
      return 1223;
     }
    }
    if(hasUser&&!bridgeRestart)throw new InvalidOperationException("Abre el instalador desde tu sesión de usuario sin ejecutarlo como administrador.");
    Application.Run(new InstallerWindow(selection,bridgeRestart?args[1]:null));return 0;
   }
   catch(Exception e){MessageBox.Show(e.Message,"Catálogo · No se puede continuar",MessageBoxButtons.OK,MessageBoxIcon.Information);return 1;}
  }
 }
 public sealed class RunResult {public int Code;public string Output;}
 public sealed class InstallerWindow:Form {
  readonly AppInfo[] apps;
  readonly string gooseBridge;
  readonly ListView list=new ListView();
  readonly Label heading=new Label(),description=new Label(),status=new Label();
  readonly ProgressBar progress=new ProgressBar();
  readonly Button stop=new Button(),logs=new Button(),help=new Button();
  bool running=true,stopRequested=false;string logPath;
  readonly Color ink=Color.FromArgb(21,36,62),blue=Color.FromArgb(37,99,235);
  public InstallerWindow(AppInfo[] selection,string bridge){
   apps=selection;gooseBridge=bridge;Text="Catálogo · Instalación personalizada";StartPosition=FormStartPosition.CenterScreen;ClientSize=new Size(660,545);
   MinimumSize=new Size(660,545);FormBorderStyle=FormBorderStyle.FixedSingle;MaximizeBox=false;BackColor=Color.FromArgb(247,249,252);
   Font=new Font("Segoe UI",10);AutoScaleMode=AutoScaleMode.Dpi;
   heading.Text="Tu PC, a tu manera.";heading.Font=new Font("Segoe UI",23,FontStyle.Bold);heading.ForeColor=ink;heading.SetBounds(28,24,610,45);
   description.Text=apps.Length+" programas seleccionados. Permiso de administrador concedido para esta instalación.";description.ForeColor=Color.FromArgb(105,119,140);description.SetBounds(30,80,596,48);
   list.SetBounds(30,140,600,220);list.View=View.Details;list.FullRowSelect=true;list.HeaderStyle=ColumnHeaderStyle.Nonclickable;list.BorderStyle=BorderStyle.FixedSingle;
   list.Columns.Add("Programa",190);list.Columns.Add("Estado",382);
   foreach(var app in apps){var row=new ListViewItem(app.name);row.SubItems.Add("En espera");list.Items.Add(row);}
   status.Text="Comprobando los requisitos…";status.ForeColor=ink;status.SetBounds(30,374,600,43);
   progress.SetBounds(30,422,600,7);progress.Style=ProgressBarStyle.Marquee;
   stop.Text="Detener después del actual";stop.SetBounds(355,454,275,37);stop.BackColor=blue;stop.ForeColor=Color.White;stop.FlatStyle=FlatStyle.Flat;stop.FlatAppearance.BorderSize=0;
   stop.Click+=(s,e)=>{if(!running){Close();return;}stopRequested=true;stop.Enabled=false;stop.Text="Se detendrá al terminar";};
   logs.Text="Ver registro";logs.SetBounds(30,454,140,37);logs.FlatStyle=FlatStyle.Flat;logs.Click+=(s,e)=>OpenLog();
   help.Text="Obtener WinGet";help.SetBounds(180,454,163,37);help.FlatStyle=FlatStyle.Flat;help.Visible=false;
   help.Click+=(s,e)=>{try{Process.Start(new ProcessStartInfo("https://aka.ms/getwinget"){UseShellExecute=true});}catch(Exception ex){MessageBox.Show(ex.Message);}};
   var note=new Label {Text="Versión de desarrollo · Descargas con comprobación de integridad",ForeColor=Color.FromArgb(133,145,163),Font=new Font("Segoe UI",8)};
   note.SetBounds(30,510,600,24);
   Controls.AddRange(new Control[]{heading,description,list,status,progress,stop,logs,help,note});
   FormClosing+=(s,e)=>{if(running){e.Cancel=true;stopRequested=true;stop.Enabled=false;stop.Text="Se detendrá al terminar";status.Text="Esperando a que termine el programa actual antes de cerrar.";}}; 
   Shown+=async(s,e)=>await RunAll();
  }
  void Log(string text){if(logPath!=null)File.AppendAllText(logPath,DateTime.Now.ToString("s")+" "+text+Environment.NewLine,Encoding.UTF8);}
  void OpenLog(){if(logPath!=null&&File.Exists(logPath))try{Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"notepad.exe"),"\""+logPath+"\""){UseShellExecute=false});}catch(Exception e){MessageBox.Show(e.Message);}}
  async Task<RunResult> Run(string winget,string arguments,int timeoutMs){
   Log("winget "+arguments);
   return await Task.Run(()=>{
    using(var process=new Process()){
     process.StartInfo=new ProcessStartInfo(winget,arguments){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};
     process.Start();
     var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();
     if(timeoutMs>0&&!process.WaitForExit(timeoutMs)){try{process.Kill();}catch{}throw new TimeoutException("WinGet no respondió a la comprobación. Revisa tu conexión y vuelve a intentarlo.");}
     if(timeoutMs==0)process.WaitForExit();
     Task.WaitAll(stdout,stderr);
     return new RunResult {Code=process.ExitCode,Output=stdout.Result+Environment.NewLine+stderr.Result};
    }
   });
  }
  async Task RunAll(){
   int failed=0,completed=0;bool restartPending=false;
   try{
    if(!Program.IsAdministrator())throw new InvalidOperationException("Se necesitan permisos de administrador antes de iniciar la instalación.");
    var dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Catalogo","Logs");Directory.CreateDirectory(dir);
    logPath=Path.Combine(dir,"instalacion-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6)+".log");
    Log("Selección: "+String.Join(", ",apps.Select(a=>a.id)));
    Log("Permisos de administrador: confirmados para toda la selección.");
    var winget=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Microsoft","WindowsApps","winget.exe");
    if(apps.Any(a=>UserAppInstaller.Route(a)==0&&!SpybotInstaller.Supports(a)&&!DirectInstaller.Supports(a)&&!CdbInstaller.Supports(a)&&!GooseInstaller.Supports(a.id,a.architecture,a.wingetId)&&!DeepSeekInstaller.Supports(a.id,a.architecture,a.wingetId)&&!AnythingInstaller.Supports(a.id,a.architecture,a.wingetId)&&!GeminiInstaller.Supports(a.id,a.architecture,a.wingetId)&&!SpotifyInstaller.Supports(a.id,a.architecture,a.wingetId)&&!ClineInstaller.Supports(a.id,a.architecture,a.wingetId))){
    if(!File.Exists(winget)){help.Visible=true;throw new InvalidOperationException("Necesitas el Instalador de aplicación de Microsoft (WinGet). Usa «Obtener WinGet», instálalo y vuelve a abrir este archivo.");}
    var version=await Run(winget,"--version",30000);Log(version.Output);
    Version parsed;
    if(version.Code!=0||!Version.TryParse(version.Output.Trim().TrimStart('v'),out parsed)||parsed<new Version(1,6))
      {help.Visible=true;throw new InvalidOperationException("Actualiza el Instalador de aplicación de Microsoft y vuelve a abrir este archivo. Se necesita WinGet 1.6 o posterior.");}
    var source=await Run(winget,"source export --name winget",30000);Log(source.Output);
    if(source.Code!=0||!Core.IsOfficialSource(source.Output.Trim()))
      throw new InvalidOperationException("El origen «winget» no coincide con el repositorio oficial de Microsoft. Revisa su configuración antes de continuar.");
    }
    for(int i=0;i<apps.Length;i++){
     if(stopRequested){for(int j=i;j<apps.Length;j++)list.Items[j].SubItems[1].Text="No iniciado · detenido";break;}
     var app=apps[i];list.Items[i].SubItems[1].Text="Descargando / instalando…";list.Items[i].ForeColor=blue;
     status.Text="("+ (i+1) +" de "+apps.Length+") "+app.name+" · Descarga, comprobación e instalación.";
     try{
      // No timeout or force-kill during an actual installation: do not leave a package half-installed.
      RunResult result;
      if(SpybotInstaller.Supports(app)){
       Core.InstallArguments(app);result=await Task.Run(()=>new RunResult {Code=SpybotInstaller.Install(app,Log),Output=""});
      }else if(DirectInstaller.Supports(app)){
       Core.InstallArguments(app); // Revalidate against the embedded catalog before executing.
       result=await Task.Run(()=>new RunResult {Code=DirectInstaller.Install(app,Log),Output=""});
      }else if(CdbInstaller.Supports(app)){
       Core.InstallArguments(app);
       result=await Task.Run(()=>new RunResult {Code=CdbInstaller.Install(app,Log),Output=""});
      }else if(GooseInstaller.Supports(app.id,app.architecture,app.wingetId)){
       Core.InstallArguments(app);
       if(String.IsNullOrEmpty(gooseBridge))throw new InvalidOperationException("Falta la sesión original del usuario.");
       result=await Task.Run(()=>{string message;var code=GooseBridge.Request(gooseBridge,out message);return new RunResult {Code=code,Output=message};});
      }else if(DeepSeekInstaller.Supports(app.id,app.architecture,app.wingetId)){
       Core.InstallArguments(app);
       if(String.IsNullOrEmpty(gooseBridge))throw new InvalidOperationException("Falta la sesión original del usuario.");
       result=await Task.Run(()=>{string message;var code=GooseBridge.Request(gooseBridge,2,out message);return new RunResult {Code=code,Output=message};});
      }else if(AnythingInstaller.Supports(app.id,app.architecture,app.wingetId)){
       Core.InstallArguments(app);
       if(String.IsNullOrEmpty(gooseBridge))throw new InvalidOperationException("Falta la sesión original del usuario.");
       result=await Task.Run(()=>{
        string message;var present=GooseBridge.Request(gooseBridge,3,out message);
        if(!String.IsNullOrEmpty(message))throw new InvalidOperationException(message);
        if(present==1)return new RunResult{Code=unchecked((int)0x8A150061),Output="AnythingLLM ya presente: se conserva sin cambios."};
        if(present!=0)throw new InvalidOperationException("No se pudo comprobar la instalación existente.");
        int dependency=AnythingRuntime.Ensure(Log);
        if(dependency==3010)restartPending=true;
        if(dependency!=0&&dependency!=3010)return new RunResult{Code=dependency,Output="Falló el componente requerido de Microsoft."};
        int code=GooseBridge.Request(gooseBridge,4,out message);
        if(code==0&&dependency==3010)code=3010;
        return new RunResult {Code=code,Output=message};
       });
      }else if(GeminiInstaller.Supports(app.id,app.architecture,app.wingetId)){
       Core.InstallArguments(app);if(String.IsNullOrEmpty(gooseBridge))throw new InvalidOperationException("Falta la sesión original del usuario.");
       result=await Task.Run(()=>{string message;var code=GooseBridge.Request(gooseBridge,5,out message);return new RunResult{Code=code,Output=message};});
      }else if(SpotifyInstaller.Supports(app.id,app.architecture,app.wingetId)){
       Core.InstallArguments(app);if(String.IsNullOrEmpty(gooseBridge))throw new InvalidOperationException("Falta la sesión original del usuario.");
       result=await Task.Run(()=>{string message;var code=GooseBridge.Request(gooseBridge,6,out message);return new RunResult{Code=code,Output=message};});
      }else if(ClineInstaller.Supports(app.id,app.architecture,app.wingetId)){
       Core.InstallArguments(app);if(String.IsNullOrEmpty(gooseBridge))throw new InvalidOperationException("Falta la sesión original del usuario.");
       result=await Task.Run(()=>{string message;var code=GooseBridge.Request(gooseBridge,7,out message);return new RunResult{Code=code,Output=message};});
      }else if(UserAppInstaller.Route(app)!=0){
       Core.InstallArguments(app);if(String.IsNullOrEmpty(gooseBridge))throw new InvalidOperationException("Falta la sesión original del usuario.");
       result=await Task.Run(()=>{string message;var code=GooseBridge.Request(gooseBridge,UserAppInstaller.Route(app),out message);return new RunResult{Code=code,Output=message};});
      }else result=await Run(winget,Core.InstallArguments(app),0);
      Log(result.Output);Log("ExitCode: "+result.Code);
      if(result.Code==3010)restartPending=true;
      list.Items[i].SubItems[1].Text=Core.Status(result.Code);
      list.Items[i].ForeColor=Core.IsSuccess(result.Code)?Color.FromArgb(32,131,100):Color.FromArgb(181,74,58);
      if(!Core.IsSuccess(result.Code))failed++;else completed++;
     }catch(Exception e){Log(e.ToString());list.Items[i].SubItems[1].Text="Error · ver registro";list.Items[i].ForeColor=Color.Firebrick;failed++;}
    }
    heading.Text=stopRequested?"Proceso detenido.":failed>0?"Revisa el resultado.":"Todo listo.";
    status.Text=completed+" completados o ya presentes · "+failed+" con errores"+(stopRequested?" · Quedaron programas sin iniciar.":".");
    if(failed>0)description.Text=restartPending?"Consulta el registro. Una instalación indicó que es necesario reiniciar el equipo.":"Puedes consultar el registro y volver a ejecutar el instalador. Los programas presentes se conservarán.";
    else description.Text=stopRequested?"Los programas ya instalados se conservan. Puedes volver a abrir este archivo.":restartPending?"Una instalación indicó que es necesario reiniciar el equipo.":"Los programas presentes se conservaron. Consulta el resultado de cada instalación.";
   }catch(Exception e){try{Log(e.ToString());}catch{}heading.Text="No se puede continuar.";status.Text="No se iniciarán más instalaciones.";description.Text=e.Message;}
   finally{running=false;progress.Style=ProgressBarStyle.Blocks;progress.Value=100;stop.Enabled=true;stop.Text="Cerrar";}
  }
 }
}
