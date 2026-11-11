using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace CodexProxyDesktop {
 public sealed class LinkFailure {public string Node="",Group="",Host="",Utc="",Kind="连接失败";public bool UserReported;}
 public sealed class StatusEvidence {
  public string Title,Short,Detail;public int Severity; // 0 unknown, 1 caution, 2 fresh probe response
 }
 public static class Evidence23 {
  public static bool Fresh(string utc,DateTime now,int seconds){var t=Files.Date(utc);return t!=DateTime.MinValue&&t<=now.AddSeconds(2)&&(now-t).TotalSeconds<=seconds;}
  public static bool Relevant(LinkFailure f,NetworkView v,DateTime now){return f!=null&&f.Node==v.Node&&f.Group==v.Group&&Fresh(f.Utc,now,45);}
  public static string Coverage(NetworkView v,Settings c){if(!c.AllowCountryFallback)return "";if(v.Mode=="rule"&&!v.RuleVerified)return "路线待确认 · 候选尚未统计";int jp=CountryPolicy.Members(v,v.Group,"JP").Length,sg=CountryPolicy.Members(v,v.Group,"SG").Length;return jp==0||sg==0?"候选不完整 · 日本 "+jp+" / 新加坡 "+sg:"";}
  public static bool AppResponse(NetworkView v,DateTime now){return v.Controller&&v.ServiceTrafficValid&&Fresh(v.TrafficUtc,now,6)&&Fresh(v.AppResponseUtc,now,15);}
  public static string Target(NetworkView v,bool chat,DateTime now){if(!Fresh(v.ServiceProbeUtc,now,35))return "未更新";int result=chat?v.ChatGptProbeResult:v.CodexProbeResult;if(result==-2)return "控制请求未确认";if(result==-1)return "内核探测失败（未提供具体阶段）";if(result>=0)return "有响应 / "+result+" ms";return v.ServiceKnown?((chat?v.ChatGptReachable:v.CodexReachable)?"有响应":"探测失败"):"未确认";}
  public static string Details(NetworkView v,DateTime now){return "已有应用连接："+(AppResponse(v,now)?"下行响应 "+Math.Max(0,(int)(now-Files.Date(v.AppResponseUtc)).TotalSeconds)+" 秒前，节点 "+DiagnosticJournal.Safe(v.AppResponseNode):"暂无近期响应证据；空闲不代表断网")+"；新连接探测：ChatGPT "+Target(v,true,now)+"，Codex 环境 "+Target(v,false,now)+"；路线："+(!Fresh(v.RouteUtc,now,8)?"未更新":v.Mode=="rule"&&!v.RuleVerified?"规则未确认，自动恢复受限（"+(String.IsNullOrEmpty(v.RuleIssue)?"缺少核验依据":v.RuleIssue)+"）":"已确认"+(String.IsNullOrEmpty(v.RuleSource)?"":" · "+v.RuleSource))+"。连接传输不验证某轮回答是否完成。";}
  public static StatusEvidence Describe(NetworkView v,Settings c,LinkFailure failure,DateTime now){var s=new StatusEvidence{Title="尚未确认",Short="未确认",Severity=0,Detail=Details(v,now)};
   if(c.Paused){s.Title=s.Short="已暂停";return s;}
   if(!v.Controller){s.Title="本地监控未连接";s.Short="监控断开";return s;}
   if(AppResponse(v,now)){bool conflict=Relevant(failure,v,now);s.Title=conflict?"应用连接有响应 · 部分连接待核对":"近期应用连接有响应";s.Short=conflict?"有响应 · 待核对":"应用有响应";s.Severity=conflict?1:2;return s;}
   if(!String.IsNullOrEmpty(v.RouteUtc)&&!Fresh(v.RouteUtc,now,8)){s.Title="路线信息已过期";s.Short="路线过期";return s;}
   if(v.Mode=="rule"&&!v.RuleVerified){bool transport=Fresh(v.AnonymousResponseUtc,now,90);s.Title=transport?"HTTPS 有响应 · 规则路径待确认":"规则路径未确认 · 使用状态未知";s.Short=transport?"有响应 · 路线待确认":"规则未确认";return s;}
   if(Relevant(failure,v,now)){s.Title=failure.UserReported?"用户报告任务异常，等待核对":"相关连接失败，当前对话待核对";s.Short=failure.UserReported?"任务异常待核对":"连接失败待核对";s.Severity=1;s.Detail=(failure.UserReported?"用户报告不能直接判定节点故障；":"相关失败线索："+failure.Kind+"；")+s.Detail;return s;}
   if(v.Checking&&(!v.ServiceKnown||!Fresh(v.ServiceProbeUtc,now,35))){s.Title="正在复测服务路径";s.Short="复测中";return s;}if(v.Checking)s.Detail="后台复查中，保留最近有效结果；"+s.Detail;
   if(!v.ServiceKnown||!Fresh(v.ServiceProbeUtc,now,35)){s.Title="服务探测未更新";s.Short="探测待更新";return s;}
   if(!v.ChatGptReachable||!v.CodexReachable){bool partial=v.ChatGptReachable||v.CodexReachable;s.Title=partial?"部分目标有响应 · 另一目标待复测":v.Failures>=2?"连续探测失败 · 使用状态待确认":"探测失败 · 等待复测";s.Short=partial?"部分目标响应":"探测失败待确认";s.Severity=1;return s;}
   s.Title="HTTP 探测响应 · 对话未核验";s.Short="HTTP 响应";s.Severity=2;return s;
  }
  // Only accept known service destinations and an explicit dial through the current manual group.
  public static LinkFailure ParseFailure(string line,NetworkView v,DateTime now){if(String.IsNullOrEmpty(line)||line.Length>16384||!v.Controller||String.IsNullOrEmpty(v.Node)||String.IsNullOrEmpty(v.Group))return null;
   Dictionary<string,object> data;try{data=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(line);}catch{return null;}
   string level=NodePolicy.Str(data,"type"),message=NodePolicy.Str(data,"payload");if(level!="warning"&&level!="error")return null;
   string scopes=Regex.Escape(v.Group);if(v.Mode=="rule"&&v.RuleVerified&&!String.IsNullOrEmpty(v.RuleRoot))scopes+="|"+Regex.Escape(v.RuleRoot);if(!Regex.IsMatch(message,@"\bdial\s+(?:"+scopes+@")(?=\s|\()",RegexOptions.IgnoreCase))return null;
   if(!Regex.IsMatch(message,@"\berror\b|\btimeout\b|\btimed out\b|\bfailed\b",RegexOptions.IgnoreCase))return null;
   var match=Regex.Match(message,@"-->\s*(?<host>[a-z0-9.-]+):\d+",RegexOptions.IgnoreCase);string host=match.Groups["host"].Value.ToLowerInvariant();if(!TrafficTracker.ServiceHost(host))return null;
   string kind=Regex.IsMatch(message,@"(?i)dns|resolve|lookup")?"域名解析失败":Regex.IsMatch(message,@"(?i)tls|certificate|handshake")?"TLS 握手失败":Regex.IsMatch(message,@"(?i)timeout|timed out|deadline")?"连接超时":Regex.IsMatch(message,@"(?i)reset|refused")?"连接拒绝或重置":"建连失败";
   return new LinkFailure{Node=v.Node,Group=v.Group,Host=host,Utc=now.ToString("o"),Kind=kind};
  }
  public static string RecoveryWait(RecoveryDecision d,NetworkState state,bool fast,DateTime now){int cooldown=fast?45:180;double remaining=(Files.Date(state.LastSwitchUtc).AddSeconds(cooldown)-now).TotalSeconds;if(remaining>0)return "切换冷却剩余 "+Math.Ceiling(remaining)+" 秒";
   if(d.NextAttempt>now)return "复测退避剩余 "+Math.Ceiling((d.NextAttempt-now).TotalSeconds)+" 秒";
   int window=fast?10:30;var attempts=(state.Switches??new List<string>()).Select(Files.Date).Where(t=>(now-t).TotalMinutes<window).OrderBy(t=>t).ToArray();return attempts.Length>=3?"切换次数达到限制，约 "+Math.Ceiling((attempts[0].AddMinutes(window)-now).TotalSeconds)+" 秒后解除":"";
  }
 }
}
