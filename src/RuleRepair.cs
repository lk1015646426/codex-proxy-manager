using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace CodexProxyDesktop {
 public sealed class RuleRepairPlan {
  public string Mode,Root,Child,Node,OldRoot,OldChild;public bool Ready,Applied,Verified;public string Detail="";
 }
 public static class RuleRepair {
  static Dictionary<string,NodeInfo> Catalog(IController c){return NodePolicy.Catalog(c.Call("/proxies","GET",null));}
  public static RuleRepairPlan Prepare(IController c){var p=new RuleRepairPlan{Mode=NodePolicy.Str(c.Call("/configs","GET",null),"mode")};var nodes=Catalog(c);var route=RuleRouting.Resolve(c.Call("/rules","GET",null),nodes);p.Root=route.Root;
   if((p.Mode!="global"&&p.Mode!="rule")||!route.Verified||p.Mode=="global"&&!nodes.ContainsKey("GLOBAL")){p.Detail="需要确认全局或规则模式的服务路径；未改动选择。";return p;}p.Node=NodePolicy.Leaf(nodes,p.Mode=="global"?"GLOBAL":route.Root);if(!nodes.ContainsKey(p.Node)||!CountryFallback.Supported(nodes[p.Node].Country)){p.Detail="当前服务节点不属于日新范围，未改动。";return p;}
   var root=nodes[p.Root];var child=root.Members.Where(n=>nodes.ContainsKey(n)&&nodes[n].Type=="Selector"&&nodes[n].Members.Contains(p.Node)&&CountryPolicy.Members(new NetworkView{Nodes=nodes},n,"JP").Length>0&&CountryPolicy.Members(new NetworkView{Nodes=nodes},n,"SG").Length>0).Select(n=>nodes[n]).FirstOrDefault();
   if(child==null){p.Detail="规则入口没有同时支持日新节点的现有手动选择组，未改写规则。";return p;}p.Child=child.Name;p.OldRoot=root.Selected;p.OldChild=child.Selected;p.Ready=true;p.Detail="将规则入口接到现有手动选择组，并沿用当前服务节点；运行模式不变。现有共享组的其他代理应用可能随组选择变化。";return p;
  }
  static bool Probe(IController c,string group,string url){try{var data=c.Call("/proxies/"+Uri.EscapeDataString(group)+"/delay?timeout=4000&url="+Uri.EscapeDataString(url),"GET",null);int delay;return NodePolicy.TryDelay(data,out delay);}catch{return false;}}
  static bool Healthy(IController c,string group){return (Probe(c,group,"https://chatgpt.com/")||Probe(c,group,"https://chatgpt.com/"))&&(Probe(c,group,"https://codex-cloud-environments.chatgpt.com/")||Probe(c,group,"https://codex-cloud-environments.chatgpt.com/"));}
  static void Select(IController c,string group,string node){c.Call("/proxies/"+Uri.EscapeDataString(group),"PUT",new {name=node});}
  public static RuleRepairPlan Apply(IController c,Func<bool> authorized=null){var p=Prepare(c);if(!p.Ready||authorized!=null&&!authorized())return p;if(!Healthy(c,p.Node)){p.Detail="当前节点服务探测未确认，未调整规则入口。";return p;}bool childWritten=false,rootWritten=false;
   try{var fresh=Prepare(c);if(!fresh.Ready||fresh.Mode!=p.Mode||fresh.Root!=p.Root||fresh.Child!=p.Child||fresh.Node!=p.Node||fresh.OldRoot!=p.OldRoot||fresh.OldChild!=p.OldChild){p.Detail="模式、节点或选择已变化，操作取消。";return p;}
    if(authorized!=null&&!authorized()){p.Detail="恢复已暂停或取消";return p;}if(p.OldChild!=p.Node){Select(c,p.Child,p.Node);childWritten=true;}if(NodePolicy.Str(c.Call("/configs","GET",null),"mode")!=p.Mode)throw new InvalidOperationException();if(authorized!=null&&!authorized())throw new InvalidOperationException();if(p.OldRoot!=p.Child){Select(c,p.Root,p.Child);rootWritten=true;}p.Applied=childWritten||rootWritten;
    var nodes=Catalog(c);var route=RuleRouting.Resolve(c.Call("/rules","GET",null),nodes);p.Verified=route.Verified&&route.Root==p.Root&&route.Owner==p.Child&&NodePolicy.Leaf(nodes,p.Root)==p.Node&&(p.Mode!="global"||NodePolicy.Leaf(nodes,"GLOBAL")==p.Node)&&NodePolicy.Str(c.Call("/configs","GET",null),"mode")==p.Mode&&Healthy(c,p.Root);
    if(!p.Verified)throw new InvalidOperationException();p.Detail="规则入口接到现有日新选择组，服务节点保持不变；未改变运行模式或规则列表。";return p;
   }catch{try{var nodes=Catalog(c);if(rootWritten&&nodes[p.Root].Selected==p.Child&&nodes[p.Child].Selected==p.Node)Select(c,p.Root,p.OldRoot);nodes=Catalog(c);if(childWritten&&nodes[p.Child].Selected==p.Node)Select(c,p.Child,p.OldChild);p.Detail="验证未通过，已尝试恢复本次更改；请核对报告。";}catch{p.Detail="验证结果不确定，保留当前选择等待核对。";}p.Verified=false;return p;}
  }
  public static int Run(bool apply){try{var client=MihomoClient.Discover(Files.Load<Settings>("settings.json"));var plan=apply?Apply(client):Prepare(client);File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"rule-route-report.json"),new JavaScriptSerializer().Serialize(plan));return apply?(plan.Verified?0:1):(plan.Ready?0:1);}catch{File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"rule-route-report.json"),"{\"Ready\":false,\"Detail\":\"控制接口读取失败，未确认任何修改。\"}");return 1;}}
 }
}
