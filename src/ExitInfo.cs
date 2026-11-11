using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace CodexProxyDesktop {
 public sealed class ExitSnapshot {
  public string Node {get;set;}public string Group {get;set;}public string Mode {get;set;}public string CountryCode {get;set;}public string MaskedIp {get;set;}public string CheckedUtc {get;set;}public string Detail {get;set;}public bool RouteMatched {get;set;}public bool Conflict {get;set;}
 }
 public static class ExitInfo {
  public const string Endpoint="https://ipwho.is/?fields=success,ip,country_code";
  public static string Mask(string value){IPAddress address;if(!IPAddress.TryParse(value,out address))return "";if(address.AddressFamily==System.Net.Sockets.AddressFamily.InterNetwork){var bytes=address.GetAddressBytes();return bytes[0]+"."+bytes[1]+".*.*";}return String.Join(":",address.ToString().Split(':').Take(2))+":*";}
  public static bool SameRoute(NetworkView a,NetworkView b){return a.Mode==b.Mode&&a.Group==b.Group&&a.Node==b.Node&&a.RuleRoot==b.RuleRoot;}
  public static ExitSnapshot Validate(Dictionary<string,object> data,NetworkView before,NetworkView after,Dictionary<string,object> connections,DateTime began){var result=new ExitSnapshot{Node=before.Node,Group=before.Group,Mode=before.Mode,CheckedUtc=DateTime.UtcNow.ToString("o"),Detail="地区资料未确认"};object success;IPAddress parsed;string code=NodePolicy.Str(data,"country_code"),ip=NodePolicy.Str(data,"ip");if(!data.TryGetValue("success",out success)||!(success is bool)||!(bool)success||!Regex.IsMatch(code,@"^[A-Z]{2}$")||!IPAddress.TryParse(ip,out parsed)||IPAddress.IsLoopback(parsed))return result;
   bool matched=SameRoute(before,after)&&NodePolicy.Array(connections,"connections").Any(raw=>{var row=raw as Dictionary<string,object>;if(row==null)return false;var meta=NodePolicy.Obj(row,"metadata");var chain=NodePolicy.Array(row,"chains").Select(Convert.ToString).ToArray();DateTime start=Files.Date(NodePolicy.Str(row,"start"));return NodePolicy.Str(meta,"host").Equals("ipwho.is",StringComparison.OrdinalIgnoreCase)&&start>=began.AddSeconds(-2)&&chain.Contains(before.Node)&&chain.Contains(before.Group);});
   result.CountryCode=code;result.MaskedIp=Mask(ip);result.RouteMatched=matched;result.Conflict=matched&&!String.IsNullOrEmpty(before.Country)&&before.Country!=code;result.Detail=matched?(result.Conflict?"地区资料与节点名称不同，建议核对线路":"此次查询链路匹配；地区资料来源 ipwho.is"):"查询已返回，但链路未绑定当前节点，不能用于核对该节点";return result;
  }
  public static ExitSnapshot Query(string proxy,NetworkView before,Func<NetworkView> after,Func<Dictionary<string,object>> connections){var request=(HttpWebRequest)WebRequest.Create(Endpoint);string connectionGroup=Guid.NewGuid().ToString("N");request.ConnectionGroupName=connectionGroup;request.Method="GET";request.Proxy=new WebProxy(proxy);request.Timeout=6000;request.ReadWriteTimeout=6000;request.AllowAutoRedirect=false;request.UseDefaultCredentials=false;request.KeepAlive=true;DateTime began=DateTime.UtcNow;
   try{using(var response=(HttpWebResponse)request.GetResponse())using(var reader=new StreamReader(response.GetResponseStream())){char[] buffer=new char[32769];int length=0,read;while(length<buffer.Length&&(read=reader.Read(buffer,length,buffer.Length-length))>0)length+=read;if(length>=buffer.Length)throw new InvalidOperationException();var data=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(new string(buffer,0,length));var observed=connections();return Validate(data,before,after(),observed,began);}}
   catch{return new ExitSnapshot{Node=before.Node,Group=before.Group,Mode=before.Mode,CheckedUtc=DateTime.UtcNow.ToString("o"),Detail="查询失败或服务暂不可用；未改变节点或模式"};}finally{try{request.ServicePoint.CloseConnectionGroup(connectionGroup);}catch{}request.Abort();}
  }
 }
}
