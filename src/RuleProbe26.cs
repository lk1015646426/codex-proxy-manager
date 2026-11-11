using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace CodexProxyDesktop {
 // Destination-only conditional rules need kernel evidence. Never skip IP rules and call that proof.
 public sealed class RuleRouteVerifier {
  readonly object gate=new object();readonly Func<IController,string,string[],string[]> probe;
  string signature="";RuleRoute proof;DateTime retry=DateTime.MinValue;
  public RuleRouteVerifier():this(Probe){}
  internal RuleRouteVerifier(Func<IController,string,string[],string[]> probe){this.probe=probe;}
  internal static RuleRoute Candidate(Dictionary<string,object> data,Dictionary<string,NodeInfo> nodes){
   var filtered=new List<object>();bool conditional=false;
   foreach(object raw in NodePolicy.Array(data,"rules")){
    var rule=raw as Dictionary<string,object>;if(rule==null)return new RuleRoute{Issue="规则数据不完整"};
    if(RuleRouting.Disabled(rule))continue;string type=NodePolicy.Str(rule,"type");
    if(type=="IPCIDR"||type=="IPCIDR6"||type=="GeoIP"||type=="IPASN"||type=="GeoSite"){conditional=true;continue;}
    if(type=="DstPort"){
     string payload=NodePolicy.Str(rule,"payload");int port;
     if(!Int32.TryParse(payload,out port))return new RuleRoute{Issue="目的端口范围规则尚未支持核验"};
     if(port!=443)continue;
     filtered.Add(new Dictionary<string,object>{{"type","Match"},{"payload",""},{"proxy",NodePolicy.Str(rule,"proxy")}});break;
    }
    if(type!="Domain"&&type!="DomainSuffix"&&type!="DomainKeyword"&&type!="Match")return new RuleRoute{Issue="前置 "+type+" 规则无法用通用探测核验"};
    filtered.Add(raw);
    if(type=="Match")break;
   }
   var result=RuleRouting.Resolve(new Dictionary<string,object>{{"rules",filtered.ToArray()}},nodes);
   if(!conditional)result.Verified=false;
   return result;
  }
  internal static string Fingerprint(Dictionary<string,object> data,Dictionary<string,NodeInfo> nodes,string proxy){
   var rules=NodePolicy.Array(data,"rules").Select(raw=>{var r=raw as Dictionary<string,object>;return r==null?new[]{"invalid"}:new[]{NodePolicy.Str(r,"type"),NodePolicy.Str(r,"payload"),NodePolicy.Str(r,"proxy"),Convert.ToString(NodePolicy.Obj(r,"extra").ContainsKey("disabled")?NodePolicy.Obj(r,"extra")["disabled"]:false)};}).ToArray();
   var catalog=nodes.OrderBy(p=>p.Key,StringComparer.Ordinal).Select(p=>new {p.Key,p.Value.Type,p.Value.Selected,p.Value.Members}).ToArray();
   using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(new JavaScriptSerializer{MaxJsonLength=16*1024*1024}.Serialize(new{proxy,rules,catalog}))));
  }
  public RuleRoute Resolve(IController client,Dictionary<string,object> rules,Dictionary<string,NodeInfo> nodes,string proxy,DateTime now){lock(gate){
   var direct=RuleRouting.Resolve(rules,nodes);if(direct.Verified)return direct;
   var candidate=Candidate(rules,nodes);if(!candidate.Verified){direct.Issue=String.IsNullOrEmpty(candidate.Issue)?direct.Issue:candidate.Issue;return direct;}
   string normalized=ProxyTools.Normalize(proxy);Uri uri;IPAddress ip;
   if(normalized==null||!Uri.TryCreate(normalized,UriKind.Absolute,out uri)||!IPAddress.TryParse(uri.Host.Trim('[',']'),out ip)||!IPAddress.IsLoopback(ip))return new RuleRoute{Issue="本机 HTTP 代理入口未就绪，暂不能实测规则路线"};
   string key=client.Identity+Fingerprint(rules,nodes,normalized);
   if(key!=signature){signature=key;proof=null;retry=DateTime.MinValue;}
   if(now>=retry){retry=now.AddSeconds(20);try{
     string[] chains=probe(client,normalized,RuleRouting.Hosts);
     var afterNodes=NodePolicy.Catalog(client.Call("/proxies","GET",null));var afterRules=client.Call("/rules","GET",null);
     bool same=NodePolicy.Str(client.Call("/configs","GET",null),"mode")=="rule"&&key==client.Identity+Fingerprint(afterRules,afterNodes,normalized);
     string[] expected=Chain(nodes,candidate.Root);
     if(same&&expected.Length>1&&chains!=null&&chains.Length==RuleRouting.Hosts.Length&&chains.All(c=>c==String.Join("\n",expected))){proof=new RuleRoute{Verified=true,Root=candidate.Root,Owner=candidate.Owner,Source="内核实测（HTTP CONNECT）",EvidenceUtc=DateTime.UtcNow.ToString("o")};}
     else if(!same){proof=null;return new RuleRoute{Issue="核验期间订阅、模式或节点已变化，等待重新识别"};}
     else if(chains!=null){proof=null;return new RuleRoute{Issue="服务域名实测路线不一致，停止自动接管"};}
    }catch{ /* A failed route sample is uncertainty, never node-failure evidence. */ }
   }
   if(proof!=null&&Evidence23.Fresh(proof.EvidenceUtc,now,60))return new RuleRoute{Verified=true,Root=proof.Root,Owner=proof.Owner,Source=proof.Source,EvidenceUtc=proof.EvidenceUtc};
   return new RuleRoute{Issue="前置 IP 或地区规则需要内核实测；七个服务域名的同一路线尚未全部确认"};
  }}
  internal static string[] Chain(Dictionary<string,NodeInfo> nodes,string root){var path=new List<string>();var seen=new HashSet<string>();string name=root;
   while(nodes.ContainsKey(name)&&seen.Add(name)){var n=nodes[name];path.Add(name);if(n.IsLeaf){path.Reverse();return path.ToArray();}if(n.Type!="Selector"||String.IsNullOrEmpty(n.Selected)||!n.Members.Contains(n.Selected))return new string[0];name=n.Selected;}
   return new string[0];
  }
  internal static string[] Witnesses(Dictionary<string,object> connections,Dictionary<int,string> owned,string[] hosts,DateTime began){var found=new Dictionary<string,string>();
   foreach(object raw in NodePolicy.Array(connections,"connections")){var c=raw as Dictionary<string,object>;if(c==null)continue;var meta=NodePolicy.Obj(c,"metadata");int port;string host;IPAddress source;
    if(!Int32.TryParse(NodePolicy.Str(meta,"sourcePort"),out port)||!owned.TryGetValue(port,out host)||NodePolicy.Str(meta,"host")!=host||!IPAddress.TryParse(NodePolicy.Str(meta,"sourceIP"),out source)||!IPAddress.IsLoopback(source)||NodePolicy.Str(meta,"destinationPort")!="443")continue;
    var at=Files.Date(NodePolicy.Str(c,"start"));if(at<began.AddSeconds(-1)||at>DateTime.UtcNow.AddSeconds(2))continue;
    string[] chain=NodePolicy.Array(c,"chains").Select(Convert.ToString).ToArray();if(chain.Length<2||chain.Any(n=>n.Contains("\n")))continue;
    if(found.ContainsKey(host))return null;found[host]=String.Join("\n",chain);
   }
   return hosts.All(found.ContainsKey)?hosts.Select(h=>found[h]).ToArray():null;
  }
  static string[] Probe(IController client,string proxy,string[] hosts){var sockets=new List<TcpClient>();var owned=new Dictionary<int,string>();DateTime began=DateTime.UtcNow,deadline=began.AddSeconds(4);var uri=new Uri(proxy);
   try{foreach(string host in hosts){if(DateTime.UtcNow>=deadline)return null;var socket=new TcpClient();sockets.Add(socket);var pending=socket.BeginConnect(IPAddress.Parse(uri.Host.Trim('[',']')),uri.Port,null,null);try{if(!pending.AsyncWaitHandle.WaitOne(500))return null;socket.EndConnect(pending);}finally{pending.AsyncWaitHandle.Close();}
     int port=((IPEndPoint)socket.Client.LocalEndPoint).Port;owned.Add(port,host);var stream=socket.GetStream();stream.WriteTimeout=500;byte[] request=Encoding.ASCII.GetBytes("CONNECT "+host+":443 HTTP/1.1\r\nHost: "+host+":443\r\n\r\n");stream.Write(request,0,request.Length);
    }
    while(DateTime.UtcNow<deadline){var found=Witnesses(client.Call("/connections","GET",null),owned,hosts,began);if(found!=null)return found;Thread.Sleep(100);}return null;
   }finally{foreach(var socket in sockets)socket.Close();}
  }
 }
}
