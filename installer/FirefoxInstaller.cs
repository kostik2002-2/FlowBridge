using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Forms;

namespace FlowBridge {
  public static partial class Engine {
    const string FirefoxPackageHash="7DA9235F189CF8B62D5DC82C5C3204E555112C91F3324A3CAE05F8054C5A5FB1";
    static void FirefoxActivate(AutomationElement node,AutomationElement root) {
      if(node==null)throw new InvalidOperationException("Элемент установки Firefox недоступен.");
      var hwnd=new IntPtr(root.Current.NativeWindowHandle);
      Native.SetForegroundWindow(hwnd);
      if(Native.GetForegroundWindow()!=hwnd)throw new InvalidOperationException("Firefox потерял фокус. Повторите установку.");
      var bounds=node.Current.BoundingRectangle;
      if(node.Current.IsOffscreen||bounds.IsEmpty||bounds.Width<1||bounds.Height<1)throw new InvalidOperationException("Элемент Firefox не виден.");
      node.SetFocus();Native.PressKey(node.Current.ControlType==ControlType.Hyperlink?(ushort)13:(ushort)32);
      Thread.Sleep(350);
    }
    static AutomationElement FirefoxNavigate(Browser browser,string url,bool newTab) {
      AutomationElement root;
      try{root=BrowserRoot(browser,1,false);}
      catch{Process.Start(new ProcessStartInfo(browser.Path){UseShellExecute=true});root=BrowserRoot(browser,15,false);}
      var hwnd=new IntPtr(root.Current.NativeWindowHandle);
      if(Native.IsIconic(hwnd))Native.ShowWindow(hwnd,9);
      Native.SetForegroundWindow(hwnd);Thread.Sleep(250);
      if(Native.GetForegroundWindow()!=hwnd)throw new InvalidOperationException("Firefox lost focus.");
      Native.PressKey(27);
      if(newTab){Process.Start(new ProcessStartInfo(browser.Path){Arguments="-new-tab \""+url+"\"",UseShellExecute=true});Thread.Sleep(900);return BrowserRoot(browser,10,false);}
      var address=Find(root,"urlbar-input");
      if(address==null)throw new InvalidOperationException("Адресная строка Firefox недоступна.");
      Native.ControlKey(76);Thread.Sleep(150);Native.TypeUnicode(url);
      if(Native.GetForegroundWindow()!=hwnd)throw new InvalidOperationException("Firefox потерял фокус.");
      Native.PressKey(13);Thread.Sleep(850);return root;
    }
    static AutomationElement FirefoxWait(AutomationElement root,string id,string russian,string english,int seconds) {
      var until=DateTime.UtcNow.AddSeconds(seconds);
      do{
        var nodes=root.FindAll(TreeScope.Descendants,Condition.TrueCondition);
        for(int i=0;i<nodes.Count;i++){var c=nodes[i].Current;
          if(c.IsOffscreen||c.BoundingRectangle.IsEmpty||c.BoundingRectangle.Width<1)continue;
          if((id!=null&&c.AutomationId==id)||(russian!=null&&c.Name==russian)||(english!=null&&c.Name==english))return nodes[i];
        }
        Thread.Sleep(200);
      }while(DateTime.UtcNow<until);
      return null;
    }
    static bool FirefoxInstalled(AutomationElement root) {
      // Only the active Add-ons Manager document, never recommended cards or inactive tabs.
      var docs=root.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Document));
      for(int i=0;i<docs.Count;i++){
        if(docs[i].Current.IsOffscreen)continue;
        string name=docs[i].Current.Name??"";
        if(name!="Управление дополнениями"&&name!="Add-ons Manager")continue;
        if(ExtensionLabel(docs[i])!=null)return true;
      }
      return false;
    }
    static string FirefoxInstalledVersion(AutomationElement root) {
      var links=root.FindAll(TreeScope.Descendants,new AndCondition(new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Hyperlink),new PropertyCondition(AutomationElement.NameProperty,"FlowBridge Preview")));
      for(int i=0;i<links.Count;i++){
        if(links[i].Current.IsOffscreen)continue;
        FirefoxActivate(links[i],root);Thread.Sleep(500);
        var nodes=root.FindAll(TreeScope.Descendants,Condition.TrueCondition);
        for(int j=0;j<nodes.Count;j++){var c=nodes[j].Current;if(!c.IsOffscreen&&Regex.IsMatch(c.Name??"",@"^\d+\.\d+\.\d+$"))return c.Name;}
        return null;
      }
      return null;
    }
    static string FirefoxUuid(AutomationElement root) {
      AutomationElement label=null;
      var labels=root.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.NameProperty,"flowbridge-preview@local.invalid"));
      for(int i=0;i<labels.Count;i++){var c=labels[i].Current;if(!c.IsOffscreen&&!c.BoundingRectangle.IsEmpty&&c.BoundingRectangle.Width>0){label=labels[i];break;}}
      if(label==null)return null;
      var node=label;
      for(int depth=0;depth<8&&node!=null;depth++,node=TreeWalker.ControlViewWalker.GetParent(node)){
        var texts=node.FindAll(TreeScope.Descendants,Condition.TrueCondition);
        for(int i=0;i<texts.Count;i++){
          var match=Regex.Match(texts[i].Current.Name??"",@"\b[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\b",RegexOptions.IgnoreCase);
          if(match.Success)return match.Value;
        }
        if(node.Current.ControlType==ControlType.Document)break;
      }
      return null;
    }
    static string InstallFirefox(Browser browser,Action<string> log) {
      log("Проверяем расширение: Mozilla Firefox");
      var root=FirefoxNavigate(browser,"about:addons",true);
      Thread.Sleep(900);
      if(FirefoxInstalled(root)){
        string installedVersion=FirefoxInstalledVersion(root);
        if(installedVersion==ExtensionVersion){Native.ControlKey(87);Thread.Sleep(350);return "Расширение уже установлено";}
        log("Обновляем Firefox FlowBridge до "+ExtensionVersion);
      }
      Prepare();
      string xpi=System.IO.Path.Combine(Destination,"firefox","FlowBridge-0.1.1-signed.xpi");
      using(var sha=SHA256.Create()){
        string hash=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(xpi))).Replace("-","");
        if(hash!=FirefoxPackageHash)throw new InvalidOperationException("Подписанный пакет Firefox повреждён.");
      }
      FirefoxNavigate(browser,new Uri(xpi).AbsoluteUri,false);
      DateTime until;
      var add=FirefoxWait(root,null,"Добавить «FlowBridge Preview»","Add FlowBridge Preview?",15);
      if(add==null)throw new InvalidOperationException("Firefox не показал подтверждение установки. Проверьте сообщение браузера.");
      var confirm=FirefoxWait(root,null,"Добавить","Add",3);
      until=DateTime.UtcNow.AddSeconds(8);
      while(confirm!=null&&!confirm.Current.IsEnabled&&DateTime.UtcNow<until)Thread.Sleep(200);
      FirefoxActivate(confirm,root);
      var installedNotice=FirefoxWait(root,"addon-install-description",null,null,8);
      if(installedNotice!=null){var okay=FirefoxWait(root,null,"OK","Okay",2)??FirefoxWait(root,null,"Хорошо",null,1);if(okay!=null)FirefoxActivate(okay,root);}
      until=DateTime.UtcNow.AddSeconds(15);
      do{
        Thread.Sleep(300);
        var done=Named(root,"«FlowBridge Preview» было добавлено.")??Named(root,"FlowBridge Preview was added.");
        if(done!=null){var okay=FirefoxWait(root,null,"OK","Okay",1)??FirefoxWait(root,null,"Хорошо",null,1);if(okay!=null)FirefoxActivate(okay,root);}
        if(FirefoxInstalled(root)){
          log("Firefox подтвердил установку подписанного расширения.");
          try{
            FirefoxNavigate(browser,"about:debugging#/runtime/this-firefox",false);
            string uuid=null;var uuidUntil=DateTime.UtcNow.AddSeconds(10);
            do{uuid=FirefoxUuid(root);if(uuid==null)Thread.Sleep(200);}while(uuid==null&&DateTime.UtcNow<uuidUntil);
            if(uuid==null)throw new InvalidOperationException("Не удалось определить адрес расширения Firefox.");
            FirefoxNavigate(browser,"moz-extension://"+uuid+"/complete.html",false);
            log("Подготовка новой вкладки Flow запущена в Mozilla Firefox.");
            return "Установлено";
          }catch(Exception error){log(error.Message);return "Установлено. Откройте Flow вручную.";}
        }
      }while(DateTime.UtcNow<until);
      throw new InvalidOperationException("Firefox не подтвердил установку расширения.");
    }
  }
}
