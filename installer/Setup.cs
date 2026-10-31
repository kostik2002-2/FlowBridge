using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Windows.Forms;
using Microsoft.Win32;

namespace FlowBridge {
  public sealed class Browser {
    public string Id, Name, ProcessName, Url, Path, Status;
    public bool CanInstall;
    public override string ToString(){return CanInstall?Name:Name+" — нужна подпись";}
  }
  public static class Native {
    [StructLayout(LayoutKind.Sequential)]public struct MouseInput {public int x,y;public uint data,flags,time;public UIntPtr extra;}
    [StructLayout(LayoutKind.Sequential)]public struct KeyInput {public ushort key,scan;public uint flags,time;public UIntPtr extra;}
    [StructLayout(LayoutKind.Explicit)]public struct InputUnion {[FieldOffset(0)]public MouseInput mouse;[FieldOffset(0)]public KeyInput key;}
    [StructLayout(LayoutKind.Sequential)]public struct Input {public uint type;public InputUnion data;}
    [DllImport("user32.dll",SetLastError=true)]static extern uint SendInput(uint count,Input[] inputs,int size);
    public static void ControlKey(ushort code){
      var events=new[]{
        new Input{type=1,data=new InputUnion{key=new KeyInput{key=17}}},
        new Input{type=1,data=new InputUnion{key=new KeyInput{key=code}}},
        new Input{type=1,data=new InputUnion{key=new KeyInput{key=code,flags=2}}},
        new Input{type=1,data=new InputUnion{key=new KeyInput{key=17,flags=2}}}};
      if(SendInput(4,events,Marshal.SizeOf(typeof(Input)))!=4)throw new InvalidOperationException("Не удалось отправить сочетание клавиш.");
    }
    public static void PressKey(ushort code){
      var events=new[]{new Input{type=1,data=new InputUnion{key=new KeyInput{key=code}}},new Input{type=1,data=new InputUnion{key=new KeyInput{key=code,flags=2}}}};
      if(SendInput(2,events,Marshal.SizeOf(typeof(Input)))!=2)throw new InvalidOperationException("Не удалось отправить клавишу браузеру.");
    }
    public static void TypeUnicode(string text){
      var events=new List<Input>();
      foreach(char character in text){
        events.Add(new Input{type=1,data=new InputUnion{key=new KeyInput{scan=character,flags=4}}});
        events.Add(new Input{type=1,data=new InputUnion{key=new KeyInput{scan=character,flags=6}}});
      }
      var inputs=events.ToArray();
      if(SendInput((uint)inputs.Length,inputs,Marshal.SizeOf(typeof(Input)))!=inputs.Length)throw new InvalidOperationException("Не удалось ввести адрес.");
    }

    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr h,uint flag);
    [DllImport("user32.dll")] public static extern IntPtr GetDlgItem(IntPtr h,int id);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h,int n);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
    [DllImport("kernel32.dll")] public static extern bool FreeConsole();
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern IntPtr SendMessage(IntPtr h,uint m,IntPtr w,string l);
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h,uint m,IntPtr w,IntPtr l);
  }
  public static partial class Engine {
    public const string Version="0.1.0"; // Keep existing unpacked-extension paths stable.
    public const string ExtensionVersion="0.1.1";
    public static readonly string Destination=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FlowBridge","versions",Version);
    public static List<Browser> Detect() {
      string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), pf=Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), pf86=Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), roaming=Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
      var rows=new List<Browser> {
        Make("chrome","Google Chrome","chrome","chrome://extensions/",true,new[]{pf+@"\Google\Chrome\Application\chrome.exe",pf86+@"\Google\Chrome\Application\chrome.exe",local+@"\Google\Chrome\Application\chrome.exe"}),
        Make("comet","Comet","comet","comet://extensions/",true,new[]{local+@"\Perplexity\Comet\Application\comet.exe"}),
        Make("edge","Microsoft Edge","msedge","edge://extensions/",true,new[]{pf86+@"\Microsoft\Edge\Application\msedge.exe",pf+@"\Microsoft\Edge\Application\msedge.exe"}),
        Make("opera","Opera","opera","opera://extensions/",true,new[]{local+@"\Programs\Opera\opera.exe",local+@"\Programs\Opera\launcher.exe",pf+@"\Opera\opera.exe",pf+@"\Opera\launcher.exe",pf86+@"\Opera\opera.exe",pf86+@"\Opera\launcher.exe"}),
        Make("yandex","Яндекс Браузер","browser","browser://extensions/",true,new[]{local+@"\Yandex\YandexBrowser\Application\browser.exe",pf+@"\Yandex\YandexBrowser\Application\browser.exe",pf86+@"\Yandex\YandexBrowser\Application\browser.exe"}),
        Make("firefox","Mozilla Firefox","firefox","about:addons",true,new[]{pf+@"\Mozilla Firefox\firefox.exe",pf86+@"\Mozilla Firefox\firefox.exe"})
      };
      foreach(var row in rows) {
        if(row.Path==null) {
          foreach(var p in Process.GetProcessesByName(row.ProcessName)) { try { var candidate=p.MainModule.FileName;if(File.Exists(candidate)){row.Path=candidate;break;} }catch{} }
        }
        row.Status=row.Path==null?"Браузер не найден":"Готов к пробной установке";
      }
      return rows;
    }
    static Browser Make(string id,string name,string process,string url,bool install,string[] paths) {
      string found=paths.FirstOrDefault(File.Exists);
      if(found==null) {
        foreach(var hive in new[]{Registry.CurrentUser,Registry.LocalMachine}) {
          try { using(var key=hive.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\"+process+".exe")){var candidate=key==null?null:key.GetValue("") as string;if(candidate!=null&&File.Exists(candidate.Trim('"'))){found=candidate.Trim('"');break;}} }catch{}
        }
      }
      return new Browser{Id=id,Name=name,ProcessName=process,Url=url,CanInstall=install,Path=found};
    }
    public static void Prepare() {
      foreach(var entry in Payload.Files) {
        string target=System.IO.Path.Combine(Destination,entry.Key.Replace('/',System.IO.Path.DirectorySeparatorChar));
        byte[] bytes=Convert.FromBase64String(entry.Value);
        if(File.Exists(target)) {
          using(var sha=SHA256.Create()) { if(!sha.ComputeHash(File.ReadAllBytes(target)).SequenceEqual(sha.ComputeHash(bytes)))File.WriteAllBytes(target,bytes); }
          continue;
        }
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target));
        using(var stream=new FileStream(target,FileMode.CreateNew,FileAccess.Write))stream.Write(bytes,0,bytes.Length);
      }
    }
    static AutomationElement Find(AutomationElement root,string id) {
      return root.FindFirst(TreeScope.Descendants,new PropertyCondition(AutomationElement.AutomationIdProperty,id));
    }
    static AutomationElement Named(AutomationElement root,string name) {
      return root.FindFirst(TreeScope.Descendants,new PropertyCondition(AutomationElement.NameProperty,name));
    }
    static AutomationElement BrowserButton(AutomationElement root,string name) {
      var buttons=root.FindAll(TreeScope.Descendants,new AndCondition(new PropertyCondition(AutomationElement.NameProperty,name),new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Button)));
      for(int i=0;i<buttons.Count;i++){
        var node=TreeWalker.ControlViewWalker.GetParent(buttons[i]);bool webpage=false;
        while(node!=null&&!Automation.Compare(node,root)){
          if(node.Current.ControlType==ControlType.Document){webpage=true;break;}
          node=TreeWalker.ControlViewWalker.GetParent(node);
        }
        if(!webpage)return buttons[i];
      }
      return null;
    }
    static AutomationElement ExtensionLabel(AutomationElement root) {
      var doc=root;
      return doc.FindFirst(TreeScope.Descendants,new AndCondition(new PropertyCondition(AutomationElement.NameProperty,"FlowBridge Preview"),new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Text)));
    }
    static string InstalledChromiumVersion(AutomationElement root) {
      var node=ExtensionLabel(root);
      for(int depth=0;depth<7&&node!=null;depth++,node=TreeWalker.ControlViewWalker.GetParent(node)){
        if(node.Current.ControlType==ControlType.Document)break;
        var texts=node.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Text));
        for(int i=0;i<texts.Count;i++){string text=texts[i].Current.Name??"";if(Regex.IsMatch(text,@"^\d+\.\d+\.\d+(?:\.\d+)?$"))return text;}
      }
      return null;
    }
    static string HandleExistingChromium(Browser browser,AutomationElement root,Action<string> log) {
      string version=InstalledChromiumVersion(root);
      if(version==null||version==ExtensionVersion){CloseServiceTab(root);return "Расширение уже установлено";}
      log("Обновляем FlowBridge "+version+" → "+ExtensionVersion);
      Prepare();Reload(root);Enable(root);return FinishInstallation(browser,log);
    }
    static AutomationElement Developer(AutomationElement root){return Find(root,"devMode")??Find(root,"dev-switch");}
    static AutomationElement LoadButton(AutomationElement root){return Find(root,"loadUnpacked")??Named(root,"Загрузить распакованное расширение")??Named(root,"Загрузить распакованное")??Named(root,"Load unpacked extension")??Named(root,"Load unpacked");}
    static AutomationElement BrowserRoot(Browser browser,int timeout,bool extensions) {
      DateTime until=DateTime.UtcNow.AddSeconds(timeout);
      do {
        foreach(var p in Process.GetProcessesByName(browser.ProcessName)) {
          try {
            if(p.MainWindowHandle==IntPtr.Zero)continue;
            string title=p.MainWindowTitle.ToLowerInvariant();
            if(extensions&&!title.Contains("расширени")&&!title.Contains("extensions")&&!title.Contains("дополнени"))continue;
            Native.SetForegroundWindow(p.MainWindowHandle);
            var root=AutomationElement.FromHandle(p.MainWindowHandle);
            if(!extensions || Find(root,"devMode")!=null || Find(root,"loadUnpacked")!=null)return root;
          }catch{}
        }
        Thread.Sleep(250);
      }while(DateTime.UtcNow<until);
      throw new InvalidOperationException("Страница расширений недоступна для автоматической установки. Можно установить вручную.");
    }
    static AutomationElement Open(Browser browser,string url) {return Navigate(browser,url,true);}
    static AutomationElement Navigate(Browser browser,string url,bool newTab) {
      AutomationElement root;
      try{root=BrowserRoot(browser,1,false);}
      catch{Process.Start(new ProcessStartInfo(browser.Path){UseShellExecute=true});root=BrowserRoot(browser,15,false);}
      var hwnd=new IntPtr(root.Current.NativeWindowHandle);
      if(Native.IsIconic(hwnd))Native.ShowWindow(hwnd,9);Native.SetForegroundWindow(hwnd);Thread.Sleep(250);
      if(Native.GetForegroundWindow()!=hwnd)throw new InvalidOperationException("Не удалось активировать окно браузера. Повторите установку.");
      if(browser.Id=="opera"){
        // A fresh tab gives Opera a clean address field; its current-address editor rejects UIA writes.
        var operaTab=BrowserButton(root,"Новая вкладка")??BrowserButton(root,"New tab");object tabAction;
        if(operaTab==null||!operaTab.TryGetCurrentPattern(InvokePattern.Pattern,out tabAction))throw new InvalidOperationException("Не удалось открыть новую вкладку Opera.");
        ((InvokePattern)tabAction).Invoke();Thread.Sleep(500);
        Native.TypeUnicode(url);
      }else{
        var createTab=BrowserButton(root,"Новая вкладка")??BrowserButton(root,"New tab");object invoke;
        if(newTab){
          if(createTab==null||!createTab.TryGetCurrentPattern(InvokePattern.Pattern,out invoke))throw new InvalidOperationException("Не удалось создать служебную вкладку. Открытые сайты сохранены.");
          ((InvokePattern)invoke).Invoke();Thread.Sleep(350);
        }
        AutomationElement address=null;DateTime ready=DateTime.UtcNow.AddSeconds(10);
        string addressId=browser.Id=="yandex"?"view_1018":browser.Id=="edge"?"view_1017":browser.Id=="chrome"?"view_1012":"view_1020";
        do{address=Find(root,addressId);if(address!=null&&address.Current.ControlType==ControlType.Edit)break;address=null;Thread.Sleep(200);}while(DateTime.UtcNow<ready);
        if(address==null){
          var edits=root.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Edit));
          for(int i=0;i<edits.Count;i++){var name=edits[i].Current.Name.ToLowerInvariant();if(name.Contains("адресная строка")||name.Contains("address")){address=edits[i];break;}}
        }
        if(address==null)throw new InvalidOperationException("Не удалось найти адресную строку браузера.");
        address.SetFocus();((ValuePattern)address.GetCurrentPattern(ValuePattern.Pattern)).SetValue(url);
      }
      if(Native.GetForegroundWindow()!=hwnd)throw new InvalidOperationException("Окно браузера потеряло фокус. Повторите установку.");
      SendKeys.SendWait("{ENTER}");Thread.Sleep(700);
      return root;
    }
    static void Reload(AutomationElement root) {
      var nodes=root.FindAll(TreeScope.Descendants,Condition.TrueCondition);bool inCard=false;
      for(int i=0;i<nodes.Count;i++){
        var node=nodes[i];var info=node.Current;
        if(info.AutomationId=="name"){if(inCard)break;inCard=info.Name=="FlowBridge Preview";continue;}
        if(inCard&&(info.AutomationId=="dev-reload-button"||info.AutomationId=="reloadButton"||info.Name=="Reload"||info.Name=="Перезагрузить")){
          object action;if(node.TryGetCurrentPattern(InvokePattern.Pattern,out action)){((InvokePattern)action).Invoke();Thread.Sleep(500);return;}
        }
      }
      // Browser-wide Update reloads unpacked extensions when an individual reload control is absent.
      var update=Find(root,"updateNow");object pattern;
      if(update!=null&&update.TryGetCurrentPattern(InvokePattern.Pattern,out pattern)){((InvokePattern)pattern).Invoke();Thread.Sleep(500);}
    }
    static void Enable(AutomationElement root) {
      var label=ExtensionLabel(root); if(label==null)throw new InvalidOperationException("Браузер не подтвердил загрузку расширения.");
      var edgeToggle=Named(root,"Включить FlowBridge Preview")??Named(root,"Enable FlowBridge Preview");object edgePattern;
      if(edgeToggle!=null&&edgeToggle.TryGetCurrentPattern(TogglePattern.Pattern,out edgePattern)){var toggle=(TogglePattern)edgePattern;if(toggle.Current.ToggleState==ToggleState.Off)toggle.Toggle();return;}
      var nodes=root.FindAll(TreeScope.Descendants,Condition.TrueCondition);
      bool inCard=false;
      for(int i=0;i<nodes.Count;i++) {
        var node=nodes[i];var info=node.Current;
        if(info.AutomationId=="name") {if(inCard)break;inCard=info.Name=="FlowBridge Preview";continue;}
        if(!inCard||info.AutomationId!="enableToggle")continue;
        object pattern;
        if(node.TryGetCurrentPattern(TogglePattern.Pattern,out pattern)) {var toggle=(TogglePattern)pattern;if(toggle.Current.ToggleState==ToggleState.Off)toggle.Toggle();return;}
      }
      // Never infer that it is enabled from the extension name alone.
      throw new InvalidOperationException("Расширение загружено. Проверьте его переключатель в браузере.");
    }
    static string ExtensionId(AutomationElement root) {
      var nodes=root.FindAll(TreeScope.Descendants,Condition.TrueCondition);
      bool inCard=false;AutomationElement details=null;
      for(int i=0;i<nodes.Count;i++){
        var info=nodes[i].Current;
        if(info.Name=="FlowBridge Preview"&&info.ControlType==ControlType.Text){inCard=true;continue;}
        if(!inCard)continue;
        if(info.AutomationId=="name"&&info.Name!="FlowBridge Preview")break;
        var match=Regex.Match(info.Name??"",@"\b[a-p]{32}\b");
        if(match.Success)return match.Value;
        if(info.AutomationId=="detailsButton")details=nodes[i];
      }
      if(details!=null){
        ((InvokePattern)details.GetCurrentPattern(InvokePattern.Pattern)).Invoke();Thread.Sleep(350);
        var detailNodes=root.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Text));
        for(int i=0;i<detailNodes.Count;i++){
          if(detailNodes[i].Current.IsOffscreen)continue;
          var match=Regex.Match(detailNodes[i].Current.Name??"",@"\b[a-p]{32}\b");
          if(match.Success)return match.Value;
        }
      }
      throw new InvalidOperationException("Не удалось определить установленное расширение.");
    }
    static string FinishInstallation(Browser browser,Action<string> log) {
      try {
        var root=BrowserRoot(browser,5,false);
        string id=ExtensionId(root);
        // Reuse only our installation tab. The extension closes it after preparing Flow.
        if(browser.Id=="opera"||browser.Id=="yandex"){
          Process.Start(new ProcessStartInfo(browser.Path){Arguments="--new-tab \"chrome-extension://"+id+"/complete.html?closePrevious=1\"",UseShellExecute=true});
        }else Navigate(browser,"chrome-extension://"+id+"/complete.html",false);
        log("Подготовка новой вкладки Flow запущена в "+browser.Name);
        return "Установлено";
      } catch(Exception error) {
        log("Расширение установлено, но подготовка Flow не запущена: "+error.Message);
        return "Установлено. Откройте Flow вручную.";
      }
    }
    static void CloseServiceTab(AutomationElement root) {
      var tabs=root.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.TabItem));
      for(int i=0;i<tabs.Count;i++){
        object selection;
        if(!tabs[i].TryGetCurrentPattern(SelectionItemPattern.Pattern,out selection)||!((SelectionItemPattern)selection).Current.IsSelected)continue;
        var close=Named(tabs[i],"Закрыть вкладку")??Named(tabs[i],"Close tab");object action;
        if(close!=null&&close.TryGetCurrentPattern(InvokePattern.Pattern,out action)){
          ((InvokePattern)action).Invoke();Thread.Sleep(300);return;
        }
      }
      var hwnd=new IntPtr(root.Current.NativeWindowHandle);
      if(Native.GetForegroundWindow()!=hwnd)throw new InvalidOperationException("Не удалось закрыть служебную вкладку.");
      SendKeys.SendWait("{ESC}");SendKeys.SendWait("^w");Thread.Sleep(350);
    }
    public static string Install(Browser browser,Action<string> log) {
      if(!browser.CanInstall)throw new InvalidOperationException(browser.Status);
      if(browser.Path==null)throw new InvalidOperationException("Браузер не найден на компьютере.");
      if(browser.Id=="firefox")return InstallFirefox(browser,log);
      log("Проверяем расширение: "+browser.Name);
      var root=Open(browser,browser.Url);
      if(ExtensionLabel(root)!=null)return HandleExistingChromium(browser,root,log);
      DateTime pageUntil=DateTime.UtcNow.AddSeconds(12);
      while(Developer(root)==null&&LoadButton(root)==null&&DateTime.UtcNow<pageUntil){
        if(browser.Id=="edge"){
          var menu=Named(root,"Меню расширений")??Named(root,"Extensions menu");object menuAction;
          if(menu!=null&&menu.TryGetCurrentPattern(InvokePattern.Pattern,out menuAction)){((InvokePattern)menuAction).Invoke();Thread.Sleep(300);break;}
        }
        Thread.Sleep(250);
      }
      var back=Find(root,"closeButton");
      if(back!=null){((InvokePattern)back.GetCurrentPattern(InvokePattern.Pattern)).Invoke();Thread.Sleep(250);}
      if(ExtensionLabel(root)!=null)return HandleExistingChromium(browser,root,log);
      Prepare();
      var developer=Developer(root);object toggleObject;
      if(developer!=null&&developer.TryGetCurrentPattern(TogglePattern.Pattern,out toggleObject)) {
        var toggle=(TogglePattern)toggleObject;
        if(toggle.Current.ToggleState==ToggleState.Off){
          if(!developer.Current.IsEnabled)throw new InvalidOperationException("Edge запрещает режим разработчика политикой организации.");
          toggle.Toggle();Thread.Sleep(400);
          if(toggle.Current.ToggleState!=ToggleState.On)throw new InvalidOperationException("Браузер не включил режим разработчика.");
        }
      }
      if(browser.Id=="edge"&&developer!=null&&!developer.Current.IsOffscreen){
        var menu=Named(root,"Меню расширений")??Named(root,"Extensions menu");object menuAction;
        if(menu!=null&&menu.TryGetCurrentPattern(InvokePattern.Pattern,out menuAction)){((InvokePattern)menuAction).Invoke();Thread.Sleep(300);}
      }
      var load=LoadButton(root);if(load==null)throw new InvalidOperationException("Нажмите «Загрузить распакованное расширение» вручную и выберите папку из подсказки.");
      ((InvokePattern)load.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
      AutomationElement folder=null;DateTime until=DateTime.UtcNow.AddSeconds(10);
      do{Thread.Sleep(150);folder=Find(root,"1152");}while(folder==null&&DateTime.UtcNow<until);
      if(folder==null)throw new InvalidOperationException("Не удалось открыть выбор папки расширения.");
      string extension=System.IO.Path.Combine(Destination,"chromium");
      Native.SendMessage(new IntPtr(folder.Current.NativeWindowHandle),0xC,IntPtr.Zero,extension);
      var dialog=Native.GetAncestor(new IntPtr(folder.Current.NativeWindowHandle),2);
      var select=Native.GetDlgItem(dialog,1);
      if(select==IntPtr.Zero)throw new InvalidOperationException("Выберите папку расширения в открытом окне.");
      Native.SendMessage(select,0xF5,IntPtr.Zero,IntPtr.Zero);
      until=DateTime.UtcNow.AddSeconds(12);
      do{Thread.Sleep(200);if(ExtensionLabel(root)!=null){Enable(root);return FinishInstallation(browser,log);}}while(DateTime.UtcNow<until);
      throw new InvalidOperationException("Браузер не подтвердил установку. Проверьте сообщение на странице расширений.");
    }
    public static string Check(Browser browser) {
      if(browser.Path==null)throw new InvalidOperationException("Браузер не найден.");
      if(!browser.CanInstall)throw new InvalidOperationException(browser.Status);
      var page=Open(browser,"https://flow.google.com/");
      DateTime until=DateTime.UtcNow.AddSeconds(18);
      do {
          try {
            var root=page;
            var elements=root.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Text));
            for(int i=0;i<elements.Count;i++)if(elements[i].Current.Name=="Исправление применено")return "Исправление применено · генерация не проверена";
            if(Named(root,"Проверка найдена")!=null)return "Механизм совместим · доступ к редактору ещё не проверен";
            if(Named(root,"Открыть проект")!=null || Named(root,"Что вы хотите создать?")!=null)return "Интерфейс Flow доступен · генерация не проверена";
          }catch{}
        Thread.Sleep(400);
      }while(DateTime.UtcNow<until);
      return "Flow открыт · автоматическая проверка не подтвердила результат";
    }
  }
  public sealed class SetupForm:Form {
    readonly ComboBox list=new ComboBox();
    readonly Button install=new Button();
    readonly Label status=new Label();
    public SetupForm() {
      Icon=System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);ShowIcon=true;
      Text="FlowBridge";ClientSize=new Size(360,292);StartPosition=FormStartPosition.CenterScreen;
      FormBorderStyle=FormBorderStyle.FixedSingle;MaximizeBox=false;BackColor=Color.White;
      Font=new Font("Segoe UI",10);AutoScaleMode=AutoScaleMode.Dpi;
      Controls.Add(new PictureBox{Image=Icon.ToBitmap(),SizeMode=PictureBoxSizeMode.Zoom,Bounds=new Rectangle(24,21,34,34)});
      Controls.Add(new Label{Text="FlowBridge",AutoSize=true,Font=new Font("Segoe UI",19,FontStyle.Bold),Location=new Point(68,20)});
      Controls.Add(new Label{Text="Выберите браузер",AutoSize=true,Location=new Point(24,72)});
      list.DropDownStyle=ComboBoxStyle.DropDownList;list.Bounds=new Rectangle(24,98,312,30);
      Controls.Add(new Label{Text="Перед установкой включите VPN.\nFlowBridge не заменяет VPN.",Bounds=new Rectangle(24,138,312,42),Font=new Font("Segoe UI",9),ForeColor=Color.FromArgb(70,70,70)});
      install.Text="Установить";install.Bounds=new Rectangle(24,192,312,38);
      install.FlatStyle=FlatStyle.Flat;install.FlatAppearance.BorderSize=0;install.BackColor=Color.FromArgb(25,25,25);install.ForeColor=Color.White;
      status.Bounds=new Rectangle(24,242,312,38);status.TextAlign=ContentAlignment.TopCenter;
      Controls.AddRange(new Control[]{list,install,status});AcceptButton=install;
      Shown+=(sender,e)=>{
        foreach(var b in Engine.Detect().Where(b=>b.Path!=null))list.Items.Add(b);
        if(list.Items.Count>0)list.SelectedIndex=0;
        else{install.Enabled=false;status.Text="Браузеры не найдены";}
      };
      list.SelectedIndexChanged+=(sender,e)=>{
        var browser=list.SelectedItem as Browser;
        install.Enabled=browser!=null&&browser.CanInstall;
        status.ForeColor=Color.DimGray;
        status.Text=browser!=null&&!browser.CanInstall?browser.Status:"";
      };
      install.Click+=async(sender,e)=>await InstallSelected();
    }
    static Task<string> RunInstall(Browser browser) {
      var result=new TaskCompletionSource<string>();
      var worker=new Thread(()=>{try{result.SetResult(Engine.Install(browser,message=>{}));}catch(Exception error){result.SetException(error);}});
      worker.IsBackground=true;worker.SetApartmentState(ApartmentState.STA);worker.Start();return result.Task;
    }
    async Task InstallSelected() {
      var browser=list.SelectedItem as Browser;if(browser==null)return;
      install.Enabled=list.Enabled=false;status.ForeColor=Color.DimGray;status.Text="Установка…";
      try{string result=await RunInstall(browser);status.ForeColor=Color.FromArgb(25,115,65);status.Text=result;}
      catch(Exception error){status.Text="Не удалось установить";MessageBox.Show(this,error.Message,"FlowBridge",MessageBoxButtons.OK,MessageBoxIcon.Information);}
      finally{list.Enabled=true;install.Enabled=browser.CanInstall;if(!status.Text.StartsWith("Установлено"))Activate();}
    }
  }
  static class Program {
    [STAThread]static int Main(string[] args) {
      if(args.Length>=2&&args[0]=="--diagnose") {try{var rows=Engine.Detect();File.WriteAllLines(args[1],rows.Select(b=>b.Name+" | "+(b.Path==null?"not-found":"detected")+" | "+b.Status),Encoding.UTF8);return 0;}catch{return 1;}}
      if(args.Length>=3&&(args[0]=="--install"||args[0]=="--check")) {var messages=new List<string>();try{var b=Engine.Detect().First(x=>x.Id==args[1]);messages.Add(args[0]=="--install"?Engine.Install(b,messages.Add):Engine.Check(b));File.WriteAllLines(args[2],messages,Encoding.UTF8);return 0;}catch(Exception x){messages.Add("ERROR: "+x.ToString());File.WriteAllLines(args[2],messages,Encoding.UTF8);return 1;}}
      Native.FreeConsole();Native.SetProcessDPIAware();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new SetupForm());return 0;
    }
  }
}
