using System;
using System.Collections.Generic;
using System.Linq;

namespace CodexProxyDesktop {
 public sealed class RuleRoute {
  public bool Verified;public string Root="",Owner="",Issue="",Source="规则解析",EvidenceUtc="";
 }
 public static class RuleRouting {
  internal static readonly string[] Hosts={"chatgpt.com","ws.chatgpt.com","codex-cloud-environments.chatgpt.com","chat.openai.com","api.openai.com","oaistatic.com","oaiusercontent.com"};
  internal static bool Disabled(Dictionary<string,object> rule){object value;return NodePolicy.Obj(rule,"extra").TryGetValue("disabled",out value)&&value is bool&&(bool)value;}
  public static string MatchHost(object[] rules,string host){foreach(object raw in rules){var r=raw as Dictionary<string,object>;if(r==null)return "";if(Disabled(r))continue;string type=NodePolicy.Str(r,"type"),payload=NodePolicy.Str(r,"payload").ToLowerInvariant(),target=NodePolicy.Str(r,"proxy");string h=host.ToLowerInvariant();
    bool match=type=="Domain"?h==payload:type=="DomainSuffix"?h==payload||h.EndsWith("."+payload):type=="DomainKeyword"?h.Contains(payload):type=="Match";
    if(match)return target;
    if(type!="Domain"&&type!="DomainSuffix"&&type!="DomainKeyword"&&!(type=="GeoIP"&&payload=="lan"))return "";
   }return "";}
  public static string SelectingOwner(Dictionary<string,NodeInfo> nodes,string root){var visited=new HashSet<string>();string owner="",name=root;while(nodes.ContainsKey(name)&&visited.Add(name)){var node=nodes[name];if(node.IsLeaf)return owner;if(node.Type!="Selector"||String.IsNullOrEmpty(node.Selected))return "";owner=name;name=node.Selected;}return "";}
  public static RuleRoute Resolve(Dictionary<string,object> ruleData,Dictionary<string,NodeInfo> nodes){var route=new RuleRoute();var roots=Hosts.Select(h=>MatchHost(NodePolicy.Array(ruleData,"rules"),h)).Distinct().ToArray();if(roots.Length!=1||String.IsNullOrEmpty(roots[0])){route.Issue="服务域名分流不一致或存在未确认的先行规则";return route;}route.Root=roots[0];route.Owner=SelectingOwner(nodes,route.Root);route.Verified=!String.IsNullOrEmpty(route.Owner)&&route.Root!="GLOBAL";if(!route.Verified)route.Issue="规则路径未落到可管理的手动选择组";return route;}
 }
}
