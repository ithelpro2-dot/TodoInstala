using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Drawing;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.ComponentModel;

namespace Catalogo {
 static class Program {
  const string WebsiteUrl="https://todoinstala.com/";
  internal static readonly Lazy<Icon> WindowIcon=new Lazy<Icon>(()=>Icon.ExtractAssociatedIcon(typeof(Program).Assembly.Location));
  [DllImport("kernel32.dll",SetLastError=true)]static extern bool AttachConsole(int pid);
  internal static bool IsAdministrator(){
   using(var identity=WindowsIdentity.GetCurrent())return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
  }
  static bool IsDownloadCurrent(string installerPath){
#if TODOINSTALA_PREVIEW
   return true;
#else
   return Core.IsDownloadCurrent(installerPath,DateTime.UtcNow);
#endif
  }
  static int OpenWebsite(){
   try{
    Process.Start(new ProcessStartInfo(WebsiteUrl){UseShellExecute=true});
    return 0;
   }catch(Win32Exception){
    MessageBox.Show("La descarga venció. Abre "+WebsiteUrl+" en tu navegador para descargar un instalador nuevo.","TodoInstala · Descarga vencida",MessageBoxButtons.OK,MessageBoxIcon.Information);
    return 1;
   }
  }
  static bool IsElevationSelection(string value){
   return (value.Length<=180&&value.StartsWith("TI1.",StringComparison.Ordinal))||
    System.Text.RegularExpressions.Regex.IsMatch(value,@"\A[a-f0-9-]{30,230}\z");
  }
  static DialogResult AskForCode(out string code,out PortableSelection selection){
   string acceptedCode=null;
   PortableSelection acceptedSelection=null;
   DialogResult result;
   using(var font=new Font("Segoe UI",10))
   using(var form=new Form{Text="TodoInstala · Código de instalación",Icon=WindowIcon.Value,StartPosition=FormStartPosition.CenterScreen,ClientSize=new Size(450,306),AutoScaleMode=AutoScaleMode.Dpi,FormBorderStyle=FormBorderStyle.FixedSingle,MinimizeBox=false,MaximizeBox=false,Font=font,BackColor=InstallerAppearance.Surface})
   using(var logo=InstallerAppearance.CreateLogo())
   using(var footer=InstallerAppearance.CreateIntegrityFooter())
   using(var heading=new Label{ForeColor=InstallerAppearance.SecondaryText,Text="Escribe o pega el código que te dio TodoInstala.com. Puedes usarlo en otro equipo."})
   using(var input=new TextBox{MaxLength=180,AccessibleName="Código de instalación"})
   using(var paste=InstallerAppearance.CreateHoverButton("Pegar código"))
   using(var status=new Label{ForeColor=Color.Firebrick})
   using(var install=InstallerAppearance.CreateHoverButton("Instalar"))
   using(var catalog=InstallerAppearance.CreateHoverButton("Elegir en catálogo"))
   using(var cancel=InstallerAppearance.CreateHoverButton("Cancelar")){
    logo.SetBounds(28,12,210,70);
    heading.SetBounds(30,86,390,42);
    input.SetBounds(30,137,263,26);
    paste.SetBounds(301,132,119,34);
    status.SetBounds(30,173,390,43);
    catalog.SetBounds(30,222,157,34);
    cancel.SetBounds(199,222,104,34);
    install.SetBounds(315,222,105,34);
    footer.SetBounds(4,270,416,30);
    catalog.DialogResult=DialogResult.Retry;
    cancel.DialogResult=DialogResult.Cancel;
    install.Enabled=false;
    input.TextChanged+=(sender,e)=>{
     install.Enabled=!String.IsNullOrWhiteSpace(input.Text);
     status.Text="";
    };
    paste.Click+=(sender,e)=>{
     try{
      string pasted=Clipboard.GetText().Trim();
      if(pasted.Length==0||pasted.Length>input.MaxLength){
       status.Text="Copia el código completo desde la página o escríbelo aquí.";
       return;
      }
      input.Text=pasted;
      input.Focus();
      input.SelectionStart=input.TextLength;
     }catch(ExternalException){
      status.Text="El portapapeles está ocupado. Intenta de nuevo o escribe el código.";
     }
    };
    install.Click+=(sender,e)=>{
     string entered=input.Text.Trim();
     try{
      var decoded=Core.DecodePortableCode(entered);
      if(!Core.IsCurrent(decoded.IssuedUtc,DateTime.UtcNow)){
       status.Text="El código venció o la fecha del equipo no coincide. Revisa el reloj o genera otro en TodoInstala.com.";
       return;
      }
      acceptedCode=entered;
      acceptedSelection=decoded;
      form.DialogResult=DialogResult.OK;
     }catch(InvalidDataException error){status.Text=error.Message;}
    };
    form.Controls.AddRange(new Control[]{logo,heading,input,paste,status,catalog,cancel,install,footer});
    form.AcceptButton=install;form.CancelButton=cancel;form.ActiveControl=input;
    result=form.ShowDialog();
   }
   code=acceptedCode;
   selection=acceptedSelection;
   return result;
  }
  static List<IGrouping<string,AppInfo>>[] CatalogColumns(AppInfo[] choices){
   // Keep the main category columns in the same order as public/app.js.
   var main=new[]{
    new[]{"Navegadores","Inteligencia artificial","Multimedia","Almacenamiento en línea"},
    new[]{"Documentos","Utilidades","Compresión","Desarrollo"},
    new[]{"Seguridad","Imagen y diseño","Mensajería","Gaming"}
   };
   var remaining=choices.GroupBy(app=>String.IsNullOrEmpty(app.category)?"Otros":app.category)
    .ToDictionary(group=>group.Key,StringComparer.Ordinal);
   var columns=main.Select(names=>names.Where(remaining.ContainsKey).Select(name=>remaining[name]).ToList()).ToArray();
   foreach(var name in main.SelectMany(column=>column))remaining.Remove(name);
   string[] extra={"Impresión 3D","Visual C++","Otros","Java",".NET","Intercambio de archivos"};
   foreach(var name in extra.Concat(remaining.Keys).Distinct(StringComparer.Ordinal)){
    IGrouping<string,AppInfo> group;
    if(!remaining.TryGetValue(name,out group))continue;
    // Headers and their spacing take about as much room as two program rows.
    columns.OrderBy(column=>column.Sum(category=>category.Count()+2)).First().Add(group);
   }
   return columns;
  }

  static AppInfo[] ConfirmSelection(){
   const int columnWidth=148;
   var choices=Core.SelectableApps();
   var selected=new HashSet<AppInfo>();
   using(var form=new Form{Text="TodoInstala · Catálogo",Icon=WindowIcon.Value,StartPosition=FormStartPosition.CenterScreen,AutoScaleMode=AutoScaleMode.Dpi,MinimizeBox=false})
   using(var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(10,12,8,10),ColumnCount=1,RowCount=3})
   using(var heading=new Label{Dock=DockStyle.Fill,Text="Elige los programas que quieres instalar.",TextAlign=ContentAlignment.MiddleLeft})
   using(var scroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true,BackColor=SystemColors.Window})
   using(var columns=new TableLayoutPanel{Dock=DockStyle.Top,Padding=new Padding(6,4,0,8),ColumnCount=3,RowCount=1})
   using(var categoryFont=new Font(form.Font,FontStyle.Bold))
   using(var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,WrapContents=false})
   using(var install=new Button{Text="Instalar",DialogResult=DialogResult.OK,Enabled=false,AutoSize=true})
   using(var cancel=new Button{Text="Cancelar",DialogResult=DialogResult.Cancel,AutoSize=true}){
    // Size the rows once before showing the catalog. Repeated AutoSize passes delay opening the dialog.
    form.SuspendLayout();
    layout.SuspendLayout();
    scroll.SuspendLayout();
    columns.SuspendLayout();
    layout.RowStyles.Add(new RowStyle(SizeType.Absolute,34));
    layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
    layout.RowStyles.Add(new RowStyle(SizeType.Absolute,40));
    var groups=CatalogColumns(choices);
    int columnsWidth=columns.Padding.Horizontal;
    int tallestColumn=0;
    for(int i=0;i<groups.Length;i++){
     int gap=i==groups.Length-1?4:8;
     columns.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,columnWidth+gap));
     columnsWidth+=columnWidth+gap;
     var stack=new Panel{Dock=DockStyle.Fill,Margin=new Padding(0,0,gap,0)};
     stack.SuspendLayout();
     int top=0;
     foreach(var group in groups[i]){
      var title=new Label{Font=categoryFont,ForeColor=Color.FromArgb(21,46,83),Text=group.Key,UseMnemonic=false};
      int titleHeight=title.GetPreferredSize(new Size(columnWidth,0)).Height;
      top+=12;
      title.SetBounds(0,top,columnWidth,titleHeight);
      top+=titleHeight+4;
      stack.Controls.Add(title);
      foreach(var app in group){
       var choice=new CheckBox{
        UseMnemonic=false,
        Text=String.IsNullOrEmpty(app.architecture)?app.name:app.name+" ("+app.architecture+")"
       };
       var textSize=TextRenderer.MeasureText(choice.Text,form.Font,
        new Size(columnWidth-24,Int32.MaxValue),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix);
       int rowHeight=textSize.Height+4;
       choice.SetBounds(0,top+1,columnWidth,rowHeight);
       top+=rowHeight+2;
       choice.CheckedChanged+=(sender,e)=>{
        if(choice.Checked)selected.Add(app);
        else selected.Remove(app);
        install.Enabled=selected.Count>0;
       };
       stack.Controls.Add(choice);
      }
     }
     stack.ResumeLayout(false);
     columns.Controls.Add(stack,i,0);
     tallestColumn=Math.Max(tallestColumn,top);
    }
    columns.Height=tallestColumn+columns.Padding.Vertical;
    scroll.Controls.Add(columns);
    buttons.Controls.Add(install);buttons.Controls.Add(cancel);
    layout.Controls.Add(heading,0,0);layout.Controls.Add(scroll,0,1);layout.Controls.Add(buttons,0,2);
    form.Controls.Add(layout);form.AcceptButton=install;form.CancelButton=cancel;
    int width=columnsWidth+layout.Padding.Horizontal+scroll.Margin.Horizontal+SystemInformation.VerticalScrollBarWidth;
    form.ClientSize=new Size(width,610);
    form.MinimumSize=new Size(form.Width,380);
    columns.ResumeLayout(false);
    scroll.ResumeLayout(false);
    layout.ResumeLayout(false);
    form.ResumeLayout(true);
    if(form.ShowDialog()!=DialogResult.OK)return null;
    return choices.Where(selected.Contains).ToArray();
   }
  }
  [STAThread] static int Main(string[] args) {
   // Inspection never launches a process, connects to the network, or installs software.
   if((args.Length==1&&args[0]=="--inspect")||(args.Length==2&&args[0]=="--inspect-code")){
    try{
     AttachConsole(-1);
     if(args[0]=="--inspect-code"){
      var portable=Core.DecodePortableCode(args[1]);
      var mode=Core.IsCurrent(portable.IssuedUtc,DateTime.UtcNow)?"automatic":"expired";
      Console.WriteLine(Core.Json.Serialize(new {mode=mode,apps=portable.Apps.Select(app=>app.id).ToArray()}));
      return 0;
     }
     var apps=Core.ReadSelection(Application.ExecutablePath);
     Console.WriteLine(Core.Json.Serialize(new {valid=true,apps=apps.Select(a=>new {id=a.id,name=a.name,wingetId=a.wingetId,arguments=Core.InstallArguments(a)}).ToArray()}));
     return 0;
    }catch(Exception e){Console.Error.WriteLine(e.Message);return 1;}
   }
   bool bridgeRestart=args.Length==3&&args[0]=="--elevated-bridge"&&System.Text.RegularExpressions.Regex.IsMatch(args[1],@"\ATodoInstala-Goose-[a-f0-9]{32}\z")&&IsElevationSelection(args[2]);
   bool resumeRequest=args.Length==1&&args[0]=="--resume-docker";
   bool elevatedRestart=(args.Length==2&&args[0]=="--elevated"&&IsElevationSelection(args[1]))||bridgeRestart||resumeRequest;
   if(args.Length>0&&!elevatedRestart)return 2;
   Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   try{
    var installerPath=Application.ExecutablePath;
    AppInfo[] selection;
    string portableCode=null;
    PortableSelection portable=null;
    var docker=(args.Length==0||resumeRequest)?Core.SelectableApps().FirstOrDefault(DockerInstaller.Supports):null;
    bool resumingDocker=docker!=null&&DockerInstaller.HasPending(installerPath);
    if(resumeRequest&&(!resumingDocker||!IsAdministrator()))return 2;
    if(resumingDocker)selection=new[]{docker};
    else if(elevatedRestart){
     var token=args[bridgeRestart?2:1];
     if(token.StartsWith("TI1.",StringComparison.Ordinal)){
      try{portable=Core.DecodePortableCode(token);}
      catch(InvalidDataException){return 2;}
      portableCode=token;
      selection=portable.Apps;
     }else selection=Core.DecodeToken(token);
    }else{
     var response=AskForCode(out portableCode,out portable);
     if(response==DialogResult.OK)selection=portable.Apps;
     else if(response==DialogResult.Retry){
      if(!IsDownloadCurrent(installerPath))return OpenWebsite();
      selection=ConfirmSelection();
      if(selection!=null)selection=Core.WithLaunchers(selection);
     }else return 0;
    }
    if(selection==null||selection.Length==0)return elevatedRestart?2:0;
    if(!resumingDocker&&(portable==null?!IsDownloadCurrent(installerPath):!Core.IsCurrent(portable.IssuedUtc,DateTime.UtcNow)))return OpenWebsite();
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
      using(var child=Process.Start(new ProcessStartInfo(Application.ExecutablePath,resumingDocker?"--resume-docker":
       (hasUser?"--elevated-bridge "+bridge.Name:"--elevated")+" "+(portableCode??Core.SelectionToken(selection))){
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
 static class InstallerAppearance {
  internal static readonly Color Surface=Color.FromArgb(247,249,252);
  internal static readonly Color Ink=Color.FromArgb(21,36,62);
  internal static readonly Color SecondaryText=Color.FromArgb(105,119,140);
  internal static readonly Color Blue=Color.FromArgb(37,99,235);

  internal static PictureBox CreateLogo(){
   var logo=new PictureBox{SizeMode=PictureBoxSizeMode.Zoom,AccessibleName="TodoInstala"};
   Bitmap bitmap;
   using(var stream=typeof(InstallerAppearance).Assembly.GetManifestResourceStream("todoinstala-logo.png"))
   using(var image=Image.FromStream(stream))bitmap=new Bitmap(image);
   logo.Image=bitmap;
   logo.Disposed+=(s,e)=>bitmap.Dispose();
   return logo;
  }

  internal static Panel CreateIntegrityFooter(){
   var footer=new Panel{TabStop=false};
   var shield=new Panel{BackColor=Color.FromArgb(240,248,245)};
   shield.SetBounds(0,0,30,30);
   shield.Paint+=PaintIntegrityShield;
   var font=new Font("Segoe UI",9);
   var note=new Label{Text="Descargas con comprobación de integridad",ForeColor=Color.FromArgb(81,105,102),TextAlign=ContentAlignment.MiddleLeft,Font=font};
   note.SetBounds(30,0,386,30);
   footer.Controls.AddRange(new Control[]{shield,note});
   footer.Disposed+=(s,e)=>font.Dispose();
   return footer;
  }

  static void PaintIntegrityShield(object sender,PaintEventArgs e){
   var shield=(Control)sender;
   var graphics=e.Graphics;
   graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
   graphics.ScaleTransform(shield.ClientSize.Width/30f,shield.ClientSize.Height/30f);
   using(var pen=new Pen(Color.FromArgb(48,146,126),1.65f)){
    pen.LineJoin=System.Drawing.Drawing2D.LineJoin.Round;
    pen.StartCap=pen.EndCap=System.Drawing.Drawing2D.LineCap.Round;
    graphics.DrawPolygon(pen,new[]{new PointF(15,6),new PointF(22,9),new PointF(22,15),new PointF(20,19),new PointF(15,24),new PointF(10,19),new PointF(8,15),new PointF(8,9)});
    graphics.DrawLines(pen,new[]{new PointF(11.6f,14.6f),new PointF(14,17),new PointF(18.5f,12.5f)});
   }
  }

  internal static Button CreateHoverButton(string text){
   var button=new Button{Text=text,FlatStyle=FlatStyle.Flat,UseVisualStyleBackColor=false};
   var outline=Color.FromArgb(184,196,214);
   Action<bool> paint=hover=>{
    var fill=hover?Blue:Color.White;
    button.BackColor=fill;
    button.ForeColor=hover?Color.White:Ink;
    button.FlatAppearance.BorderColor=hover?Blue:outline;
    button.FlatAppearance.MouseOverBackColor=fill;
    button.FlatAppearance.MouseDownBackColor=fill;
   };
   paint(false);
   button.MouseEnter+=(s,e)=>paint(button.Enabled);
   button.MouseLeave+=(s,e)=>paint(false);
   button.EnabledChanged+=(s,e)=>paint(button.Enabled&&button.IsHandleCreated&&
    button.ClientRectangle.Contains(button.PointToClient(Cursor.Position)));
   return button;
  }
 }

 public sealed class InstallerWindow:Form {
  readonly AppInfo[] apps;
  readonly string gooseBridge;
  readonly ListView list=new ListView();
  readonly Label description=new Label(),status=new Label();
  readonly ProgressBar progress=new ProgressBar();
  readonly Button stop=new Button(),help=new Button();
  readonly CancellationTokenSource cancellation=new CancellationTokenSource();
  bool running=true;string logPath;
  public InstallerWindow(AppInfo[] selection,string bridge){
   apps=selection;gooseBridge=bridge;Text="Todoinstala · Instalación Personalizada";Icon=Program.WindowIcon.Value;StartPosition=FormStartPosition.CenterScreen;
   FormBorderStyle=FormBorderStyle.FixedSingle;MaximizeBox=false;BackColor=InstallerAppearance.Surface;
   Font=new Font("Segoe UI",10);AutoScaleMode=AutoScaleMode.Dpi;
   int rows=Math.Max(4,Math.Min(10,apps.Length+1));
   var logo=InstallerAppearance.CreateLogo();
   logo.SetBounds(28,12,210,70);
   description.Text=apps.Length==1?"1 programa seleccionado.":apps.Length+" programas seleccionados.";
   description.ForeColor=InstallerAppearance.SecondaryText;description.UseMnemonic=false;description.SetBounds(30,86,390,36);
   list.SetBounds(30,126,390,22+rows*(Font.Height+3));list.View=View.Details;list.FullRowSelect=true;list.HeaderStyle=ColumnHeaderStyle.Nonclickable;list.BorderStyle=BorderStyle.FixedSingle;
   list.Columns.Add("Programa",133);list.Columns.Add("Estado",235);
   foreach(var app in apps){var row=new ListViewItem(app.name);row.SubItems.Add("En espera");list.Items.Add(row);}
   status.Text="Comprobando los requisitos…";status.ForeColor=InstallerAppearance.Ink;status.SetBounds(30,list.Bottom+9,390,36);
   progress.SetBounds(30,list.Bottom+51,390,7);progress.Style=ProgressBarStyle.Marquee;
   stop.Text="Detener";stop.SetBounds(245,list.Bottom+68,175,34);stop.BackColor=InstallerAppearance.Blue;stop.ForeColor=Color.White;stop.FlatStyle=FlatStyle.Flat;stop.FlatAppearance.BorderSize=0;
   stop.Click+=(s,e)=>{if(running)StopInstallation();else Close();};
   help.Text="Obtener WinGet";help.SetBounds(30,list.Bottom+68,163,34);help.FlatStyle=FlatStyle.Flat;help.Visible=false;
   help.Click+=(s,e)=>{try{Process.Start(new ProcessStartInfo("https://aka.ms/getwinget"){UseShellExecute=true});}catch(Exception ex){MessageBox.Show(ex.Message);}};
   var footer=InstallerAppearance.CreateIntegrityFooter();
   footer.SetBounds(4,list.Bottom+108,416,30);
   ClientSize=new Size(450,list.Bottom+143);
   Controls.AddRange(new Control[]{logo,description,list,status,progress,stop,help,footer});
   Load+=(s,e)=>{
    if(list.Items.Count==0)return;
    var firstRow=list.GetItemRect(0);
    // Use the native row height after Windows applies its display scaling.
    int delta=firstRow.Top+rows*firstRow.Height-list.ClientSize.Height;
    if(delta==0)return;
    list.Height+=delta;
    foreach(var control in new Control[]{status,progress,stop,help,footer})control.Top+=delta;
    ClientSize=new Size(ClientSize.Width,ClientSize.Height+delta);
   };
   Load+=(s,e)=>{
    int minimumHeight=description.Height;
    description.TextChanged+=(sender,args)=>{
     var textSize=TextRenderer.MeasureText(description.Text,description.Font,new Size(description.ClientSize.Width,Int32.MaxValue),
      TextFormatFlags.WordBreak|TextFormatFlags.TextBoxControl|TextFormatFlags.NoPrefix);
     int height=Math.Max(minimumHeight,textSize.Height);
     int delta=height-description.Height;
     if(delta==0)return;
     description.Height=height;
     foreach(var control in new Control[]{list,status,progress,stop,help,footer})control.Top+=delta;
     ClientSize=new Size(ClientSize.Width,ClientSize.Height+delta);
    };
   };
   FormClosing+=(s,e)=>{if(running){e.Cancel=true;StopInstallation();}};
   FormClosed+=(s,e)=>cancellation.Dispose();
   Shown+=async(s,e)=>await RunAll();
  }
  void Log(string text){if(logPath!=null)File.AppendAllText(logPath,DateTime.Now.ToString("s")+" "+text+Environment.NewLine,Encoding.UTF8);}
  void StopInstallation(){
   if(cancellation.IsCancellationRequested)return;
   stop.Enabled=false;stop.Text="Deteniendo…";
   status.Text="Cancelando la descarga o instalación en curso…";
   cancellation.Cancel();
   if(!String.IsNullOrEmpty(gooseBridge)){
    // The user-side bridge may already have completed its final request.
    try{GooseBridge.SignalStop(gooseBridge);}catch(WaitHandleCannotBeOpenedException){}
   }
  }
  Action<InstallationPhase> phaseReporter;
  Task<T> Work<T>(Func<T> work){
   var report=phaseReporter;
   return Task.Run(()=>InstallationWork.Run(cancellation.Token,work,report));
  }
  async Task<RunResult> Run(string winget,string arguments,int timeoutMs){
   Log("winget "+arguments);
   return await Work(()=>{
    var info=new ProcessStartInfo(winget,arguments){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};
    using(var process=InstallationWork.Start(info)){
     if(process==null)throw new InvalidOperationException("No se pudo iniciar WinGet.");
     var stdout=arguments.StartsWith("install ",StringComparison.Ordinal)?
      InstallationWork.ReadWinGetOutput(process.StandardOutput):process.StandardOutput.ReadToEndAsync();
     var stderr=process.StandardError.ReadToEndAsync();
     if(timeoutMs>0&&!process.WaitForExit(timeoutMs)){try{process.Kill();}catch{}throw new TimeoutException("WinGet no respondió a la comprobación. Revisa tu conexión y vuelve a intentarlo.");}
     if(timeoutMs==0)process.WaitForExit();
     Task.WaitAll(stdout,stderr);
     return new RunResult {Code=process.ExitCode,Output=stdout.Result+Environment.NewLine+stderr.Result};
    }
   });
  }
  async Task RunAll(){
   int failed=0,completed=0,assistants=0,active=-1;bool restartPending=false,dockerPending=false,steamSignInPending=false;
   var completedIds=new HashSet<string>(StringComparer.Ordinal);
   const string steamLicenseNotice="Se requiere una licencia en tu biblioteca de Steam.";
   bool hasLicensedGame=apps.Any(UserAppInstaller.RequiresSteamLicense);
   try{
    if(!Program.IsAdministrator())throw new InvalidOperationException("Se necesitan permisos de administrador antes de iniciar la instalación.");
    var dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Catalogo","Logs");Directory.CreateDirectory(dir);
    logPath=Path.Combine(dir,"instalacion-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6)+".log");
    Log("Selección: "+String.Join(", ",apps.Select(a=>a.id)));
    Log("Permisos de administrador: confirmados para toda la selección.");
    var winget=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Microsoft","WindowsApps","winget.exe");
    if(apps.Any(a=>UserAppInstaller.Route(a)==0&&!DockerInstaller.Supports(a)&&!SpybotInstaller.Supports(a)&&!DirectInstaller.Supports(a)&&!CdbInstaller.Supports(a)&&!GooseInstaller.Supports(a.id,a.architecture,a.wingetId)&&!DeepSeekInstaller.Supports(a.id,a.architecture,a.wingetId)&&!AnythingInstaller.Supports(a.id,a.architecture,a.wingetId)&&!GeminiInstaller.Supports(a.id,a.architecture,a.wingetId)&&!SpotifyInstaller.Supports(a.id,a.architecture,a.wingetId)&&!ClineInstaller.Supports(a.id,a.architecture,a.wingetId))){
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
     cancellation.Token.ThrowIfCancellationRequested();
     var app=apps[i];
     if(!String.IsNullOrEmpty(app.requiresLauncher)&&!completedIds.Contains(app.requiresLauncher)){
      var launcher=app.requiresLauncher=="steam"?"Steam":"Epic Games Launcher";
      list.Items[i].SubItems[1].Text="No iniciado · falta "+launcher;
      list.Items[i].ForeColor=Color.Firebrick;
      Log(app.name+": se omitió porque "+launcher+" no quedó disponible.");
      failed++;
      continue;
     }
     active=i;
     int row=i;
     IProgress<InstallationPhase> progressReporter=new Progress<InstallationPhase>(phase=>{
      if(active==row&&!cancellation.IsCancellationRequested)
       list.Items[row].SubItems[1].Text=phase==InstallationPhase.Downloading?"Descargando":"Instalando";
     });
     phaseReporter=progressReporter.Report;
     list.Items[i].SubItems[1].Text="Preparando…";list.Items[i].ForeColor=InstallerAppearance.Blue;
     bool requiresLicense=UserAppInstaller.RequiresSteamLicense(app);
     if(requiresLicense)description.Text=steamLicenseNotice;
     status.Text="("+ (i+1) +" de "+apps.Length+") "+app.name+(requiresLicense?" · Abriendo en Steam.":" · Descarga, comprobación e instalación.");
     try{
      // No timeout or force-kill during an actual installation: do not leave a package half-installed.
      RunResult result;
      if(DockerInstaller.Supports(app)){
       Core.InstallArguments(app);
       result=await Work(()=>new RunResult{Code=DockerInstaller.Install(app,Application.ExecutablePath,Log),Output=""});
      }else if(SpybotInstaller.Supports(app)){
       Core.InstallArguments(app);result=await Work(()=>new RunResult {Code=SpybotInstaller.Install(app,Log),Output=""});
      }else if(DirectInstaller.Supports(app)){
       Core.InstallArguments(app); // Revalidate against the embedded catalog before executing.
       result=await Work(()=>new RunResult {Code=DirectInstaller.Install(app,Log),Output=""});
      }else if(CdbInstaller.Supports(app)){
       Core.InstallArguments(app);
       result=await Work(()=>new RunResult {Code=CdbInstaller.Install(app,Log),Output=""});
      }else if(GooseInstaller.Supports(app.id,app.architecture,app.wingetId)){
       Core.InstallArguments(app);
       if(String.IsNullOrEmpty(gooseBridge))throw new InvalidOperationException("Falta la sesión original del usuario.");
       result=await Work(()=>{string message;var code=GooseBridge.Request(gooseBridge,out message);return new RunResult {Code=code,Output=message};});
      }else if(DeepSeekInstaller.Supports(app.id,app.architecture,app.wingetId)){
       Core.InstallArguments(app);
       if(String.IsNullOrEmpty(gooseBridge))throw new InvalidOperationException("Falta la sesión original del usuario.");
       result=await Work(()=>{string message;var code=GooseBridge.Request(gooseBridge,2,out message);return new RunResult {Code=code,Output=message};});
      }else if(AnythingInstaller.Supports(app.id,app.architecture,app.wingetId)){
       Core.InstallArguments(app);
       if(String.IsNullOrEmpty(gooseBridge))throw new InvalidOperationException("Falta la sesión original del usuario.");
       result=await Work(()=>{
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
       result=await Work(()=>{string message;var code=GooseBridge.Request(gooseBridge,5,out message);return new RunResult{Code=code,Output=message};});
      }else if(SpotifyInstaller.Supports(app.id,app.architecture,app.wingetId)){
       Core.InstallArguments(app);if(String.IsNullOrEmpty(gooseBridge))throw new InvalidOperationException("Falta la sesión original del usuario.");
       result=await Work(()=>{string message;var code=GooseBridge.Request(gooseBridge,6,out message);return new RunResult{Code=code,Output=message};});
      }else if(ClineInstaller.Supports(app.id,app.architecture,app.wingetId)){
       Core.InstallArguments(app);if(String.IsNullOrEmpty(gooseBridge))throw new InvalidOperationException("Falta la sesión original del usuario.");
       result=await Work(()=>{string message;var code=GooseBridge.Request(gooseBridge,7,out message);return new RunResult{Code=code,Output=message};});
      }else if(UserAppInstaller.Route(app)!=0){
       Core.InstallArguments(app);if(String.IsNullOrEmpty(gooseBridge))throw new InvalidOperationException("Falta la sesión original del usuario.");
       result=await Work(()=>{string message;var code=GooseBridge.Request(gooseBridge,UserAppInstaller.Route(app),out message);return new RunResult{Code=code,Output=message};});
      }else result=await Run(winget,Core.InstallArguments(app),0);
      Log(result.Output);Log("ExitCode: "+result.Code);
      if(DockerInstaller.Supports(app)&&result.Code==DockerInstaller.Deferred){
       dockerPending=true;
       restartPending=true;
       list.Items[i].SubItems[1].Text="WSL 2 Ya se Instaló.";
       list.Items[i].ForeColor=Color.FromArgb(32,131,100);
       active=-1;
       continue;
      }
      if(UserAppInstaller.IsAssistant(app)&&result.Code==UserAppInstaller.SteamSignInRequired){
       steamSignInPending=true;
       list.Items[i].SubItems[1].Text="Inicia sesión en Steam";
       list.Items[i].ForeColor=InstallerAppearance.Blue;
       phaseReporter=null;
       active=-1;
       continue;
      }
      if(UserAppInstaller.IsAssistant(app)&&result.Code==UserAppInstaller.AssistantOpened){
       assistants++;
       list.Items[i].SubItems[1].Text=requiresLicense?"Enviado a Steam":"Asistente abierto";
       list.Items[i].ForeColor=InstallerAppearance.Blue;
       phaseReporter=null;
       active=-1;
       continue;
      }
      if(result.Code==3010)restartPending=true;
      list.Items[i].SubItems[1].Text=Core.Status(result.Code);
      list.Items[i].ForeColor=Core.IsSuccess(result.Code)?Color.FromArgb(32,131,100):Color.FromArgb(181,74,58);
      if(!Core.IsSuccess(result.Code))failed++;
      else{completed++;completedIds.Add(app.id);}
     }catch(OperationCanceledException){throw;}
     catch(Exception e){Log(e.ToString());list.Items[i].SubItems[1].Text="Error de instalación";list.Items[i].ForeColor=Color.Firebrick;failed++;}
     phaseReporter=null;
     active=-1;
    }
    var resultHeading=failed>0?"Revisa el resultado.":dockerPending||assistants>0||steamSignInPending?"Cola completada.":"Todo listo.";
    status.Text=resultHeading+" "+completed+" completados o ya presentes · "+failed+" con errores."+(dockerPending?" · Docker Desktop pendiente.":"");
    if(!dockerPending)description.Text=restartPending?"Hay un reinicio pendiente.":steamSignInPending?"Inicia sesión en Steam. Si falta algún juego, vuelve a abrir TodoInstala.":hasLicensedGame?steamLicenseNotice:assistants>0?"Confirma la descarga de los juegos en su asistente.":failed>0?"Vuelve a abrir TodoInstala para reintentar.":"Los programas presentes se conservaron.";
   }catch(OperationCanceledException){
    if(active>=0){
     list.Items[active].SubItems[1].Text="Cancelación solicitada";
     list.Items[active].ForeColor=InstallerAppearance.Ink;
    }
    foreach(ListViewItem item in list.Items)
     if(item.SubItems[1].Text=="En espera")item.SubItems[1].Text="No iniciado";
    status.Text="Cola detenida.";
    if(!dockerPending)description.Text=active>=0?"El instalador externo puede tardar en detenerse.":"Los programas ya instalados se conservan.";
   }catch(Exception e){try{Log(e.ToString());}catch{}status.Text="No se puede continuar.";description.Text=e.Message;}
   finally{
    phaseReporter=null;
    if(dockerPending){description.ForeColor=Color.Firebrick;description.Text=DockerInstaller.RestartNotice;}
    running=false;progress.Style=ProgressBarStyle.Blocks;progress.Value=cancellation.IsCancellationRequested?0:100;stop.Enabled=true;stop.Text="Cerrar";
   }
  }
 }
}
