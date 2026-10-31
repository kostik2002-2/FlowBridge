using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Automation;

namespace FlowBridge {
  public static partial class Engine {
    const string FirefoxPackageHash="7DA9235F189CF8B62D5DC82C5C3204E555112C91F3324A3CAE05F8054C5A5FB1";
    const string FirefoxAddonId="flowbridge-preview@local.invalid";
    static bool FirefoxVisible(AutomationElement node) {
      var c=node.Current;return !c.IsOffscreen&&!c.BoundingRectangle.IsEmpty&&c.BoundingRectangle.Width>0;
    }
    static string FirefoxAddress(AutomationElement root) {
      var address=Find(root,"urlbar-input");object value;
      return address!=null&&address.TryGetCurrentPattern(ValuePattern.Pattern,out value)?((ValuePattern)value).Current.Value:"";
    }
    static AutomationElement FirefoxAddonsDocument(AutomationElement root) {
      if(!FirefoxAddress(root).StartsWith("about:addons",StringComparison.OrdinalIgnoreCase))return null;
      var docs=root.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Document));
      for(int i=0;i<docs.Count;i++){
        string name=docs[i].Current.Name??"";
        if(FirefoxVisible(docs[i])&&(name=="Управление дополнениями"||name=="Add-ons Manager"))return docs[i];
      }
      return null;
    }
    static void FirefoxActivate(AutomationElement node,AutomationElement root) {
      if(node==null)throw new InvalidOperationException("Элемент установки Firefox недоступен.");
      var hwnd=new IntPtr(root.Current.NativeWindowHandle);
      Native.SetForegroundWindow(hwnd);
      if(Native.GetForegroundWindow()!=hwnd)throw new InvalidOperationException("Firefox потерял фокус. Повторите установку.");
      if(!FirefoxVisible(node))throw new InvalidOperationException("Элемент Firefox не виден.");
      node.SetFocus();Native.PressKey(node.Current.ControlType==ControlType.Hyperlink?(ushort)13:(ushort)32);
      Thread.Sleep(350);
    }
    static AutomationElement FirefoxNavigate(Browser browser,string url,bool newTab) {
      return Navigate(browser,url,newTab);
    }
    static AutomationElement FirefoxWait(AutomationElement root,string id,string russian,string english,int seconds) {
      var until=DateTime.UtcNow.AddSeconds(seconds);
      do{
        var nodes=root.FindAll(TreeScope.Descendants,Condition.TrueCondition);
        for(int i=0;i<nodes.Count;i++){var c=nodes[i].Current;
          if(!FirefoxVisible(nodes[i]))continue;
          if((id!=null&&c.AutomationId==id)||(russian!=null&&c.Name==russian)||(english!=null&&c.Name==english))return nodes[i];
        }
        Thread.Sleep(200);
      }while(DateTime.UtcNow<until);
      return null;
    }
    static bool FirefoxInstalled(AutomationElement root) {
      var doc=FirefoxAddonsDocument(root);
      return doc!=null&&ExtensionLabel(doc)!=null;
    }
    static string FirefoxInstalledVersion(AutomationElement root) {
      var doc=FirefoxAddonsDocument(root);if(doc==null)return null;
      var links=doc.FindAll(TreeScope.Descendants,new AndCondition(new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Hyperlink),new PropertyCondition(AutomationElement.NameProperty,"FlowBridge Preview")));
      for(int i=0;i<links.Count;i++){if(FirefoxVisible(links[i])){FirefoxActivate(links[i],root);break;}}
      var until=DateTime.UtcNow.AddSeconds(5);
      do{
        doc=FirefoxAddonsDocument(root);
        if(doc!=null){
          var nodes=doc.FindAll(TreeScope.Descendants,Condition.TrueCondition);
          var versions=new System.Collections.Generic.HashSet<string>();
          for(int i=0;i<nodes.Count;i++){string name=nodes[i].Current.Name??"";if(FirefoxVisible(nodes[i])&&Regex.IsMatch(name,@"^\d+\.\d+\.\d+$"))versions.Add(name);}
          if(versions.Count==1)return versions.First();
        }
        Thread.Sleep(200);
      }while(DateTime.UtcNow<until);
      return null;
    }
    static string FirefoxUuid(AutomationElement root) {
      if(!FirefoxAddress(root).StartsWith("about:debugging",StringComparison.OrdinalIgnoreCase))return null;
      var docs=root.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Document));
      for(int d=0;d<docs.Count;d++){
        if(!FirefoxVisible(docs[d]))continue;
        var label=Named(docs[d],FirefoxAddonId);if(label==null)continue;
        var node=TreeWalker.ControlViewWalker.GetParent(label);
        // Read only the card containing our exact add-on ID. A theme or another
        // extension's UUID is never accepted; inactive documents are excluded.
        for(int depth=0;depth<8&&node!=null&&!Automation.Compare(node,docs[d]);depth++,node=TreeWalker.ControlViewWalker.GetParent(node)){
          var links=node.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Hyperlink));
          var ids=new System.Collections.Generic.HashSet<string>();
          for(int i=0;i<links.Count;i++){
            var match=Regex.Match(links[i].Current.Name??"", @"^moz-extension://([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})/manifest\.json$",RegexOptions.IgnoreCase);
            if(match.Success)ids.Add(match.Groups[1].Value);
          }
          if(ids.Count==1)return ids.First();
          if(ids.Count>1)return null;
        }
      }
      return null;
    }
    static string FinishFirefoxInstallation(Browser browser,Action<string> log) {
      try{
        var root=FirefoxNavigate(browser,"about:debugging#/runtime/this-firefox",false);
        string uuid=null;var until=DateTime.UtcNow.AddSeconds(10);
        do{uuid=FirefoxUuid(root);if(uuid==null)Thread.Sleep(200);}while(uuid==null&&DateTime.UtcNow<until);
        if(uuid==null)throw new InvalidOperationException("Не удалось определить текущий адрес расширения Firefox.");
        FirefoxNavigate(browser,"moz-extension://"+uuid+"/complete.html",false);
        until=DateTime.UtcNow.AddSeconds(15);
        do{
          string address=FirefoxAddress(root);
          if(Regex.IsMatch(address,@"^(https://)?flow\.google\.com(?:/|$)",RegexOptions.IgnoreCase)){
            log("Новая вкладка Flow подтверждена в Mozilla Firefox.");return "Установлено";
          }
          Thread.Sleep(200);
        }while(DateTime.UtcNow<until);
        throw new InvalidOperationException("Firefox не подтвердил открытие новой вкладки Flow.");
      }catch(Exception error){log(error.Message);return "Установлено. Подготовка Flow не завершена.";}
    }
    static string InstallFirefox(Browser browser,Action<string> log) {
      log("Проверяем расширение: Mozilla Firefox");
      var root=FirefoxNavigate(browser,"about:addons",true);
      var pageUntil=DateTime.UtcNow.AddSeconds(10);
      while(FirefoxAddonsDocument(root)==null&&DateTime.UtcNow<pageUntil)Thread.Sleep(200);
      if(FirefoxAddonsDocument(root)==null)throw new InvalidOperationException("Страница дополнений Firefox не загрузилась.");
      if(FirefoxInstalled(root)){
        string installedVersion=FirefoxInstalledVersion(root);
        if(installedVersion==ExtensionVersion){Native.ControlKey(87);Thread.Sleep(350);return "Расширение уже установлено";}
        log("Обновляем Firefox FlowBridge "+(installedVersion??"(версия не определена)")+" до "+ExtensionVersion);
      }
      Prepare();
      string xpi=System.IO.Path.Combine(Destination,"firefox","FlowBridge-0.1.1-signed.xpi");
      using(var sha=SHA256.Create()){
        string hash=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(xpi))).Replace("-","");
        if(hash!=FirefoxPackageHash)throw new InvalidOperationException("Подписанный пакет Firefox повреждён.");
      }
      FirefoxNavigate(browser,new Uri(xpi).AbsoluteUri,false);
      var add=FirefoxWait(root,null,"Добавить «FlowBridge Preview»","Add FlowBridge Preview?",15);
      if(add==null)throw new InvalidOperationException("Firefox не показал подтверждение установки. Проверьте сообщение браузера.");
      var confirm=FirefoxWait(root,null,"Добавить","Add",3);
      var until=DateTime.UtcNow.AddSeconds(8);
      while(confirm!=null&&!confirm.Current.IsEnabled&&DateTime.UtcNow<until)Thread.Sleep(200);
      FirefoxActivate(confirm,root);log("Подтверждение установки XPI отправлено.");
      var installedNotice=FirefoxWait(root,"addon-install-description",null,null,10);
      log(installedNotice!=null?"Firefox показал уведомление об установке.":"Уведомление об установке Firefox не появилось.");
      if(installedNotice!=null){var okay=FirefoxWait(root,null,"OK","Okay",2)??FirefoxWait(root,null,"Хорошо",null,1);if(okay!=null)FirefoxActivate(okay,root);}
      // An old card can remain in the accessibility tree during XPI replacement.
      // Reopen the manager in the same service tab, then verify the actual version.
      FirefoxNavigate(browser,"about:addons",false);
      until=DateTime.UtcNow.AddSeconds(15);
      do{
        if(FirefoxInstalled(root)){
          string installedVersion=FirefoxInstalledVersion(root);
          if(installedVersion==ExtensionVersion){
            log("Firefox подтвердил установленную версию "+installedVersion+".");
            return FinishFirefoxInstallation(browser,log);
          }
        }
        Thread.Sleep(300);
      }while(DateTime.UtcNow<until);
      throw new InvalidOperationException("Firefox не подтвердил обновление до "+ExtensionVersion+".");
    }
  }
}
