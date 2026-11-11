using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace CodexProxyDesktop {
 public static class CountryPolicy {
  public static string Scope(string group,string country){return new JavaScriptSerializer().Serialize(new[]{group??"",country??""});}
  public static string[] Members(NetworkView view,string group,string country){if(String.IsNullOrEmpty(country)||!view.Nodes.ContainsKey(group??""))return new string[0];return view.Nodes[group].Members.Where(n=>view.Nodes.ContainsKey(n)&&view.Nodes[n].IsLeaf&&view.Nodes[n].Country==country).Distinct().ToArray();}
  public static string[] Effective(NetworkView view,Settings settings){if(!settings.AutoCountryWhitelist)return settings.AllowedNodes??new string[0];string group=String.IsNullOrEmpty(view.Group)?settings.ManagedGroup:view.Group;string[] excluded;var map=settings.CountryExclusions??new Dictionary<string,string[]>();if(!map.TryGetValue(Scope(group,settings.LockedCountry),out excluded))excluded=new string[0];return Members(view,group,settings.LockedCountry).Except(excluded??new string[0]).ToArray();}
  public static string[] ForCountry(NetworkView view,Settings settings,string country){var copy=new JavaScriptSerializer().Deserialize<Settings>(new JavaScriptSerializer().Serialize(settings));copy.LockedCountry=country;return Effective(view,copy);}
  public static void SetSelection(Settings settings,NetworkView view,string group,string country,string[] selected){settings.ManagedGroup=group;settings.LockedCountry=country;settings.AllowedNodes=selected??new string[0];if(settings.CountryExclusions==null)settings.CountryExclusions=new Dictionary<string,string[]>();if(settings.AutoCountryWhitelist)settings.CountryExclusions[Scope(group,country)]=Members(view,group,country).Except(settings.AllowedNodes).ToArray();}
 }
 public static class LaunchPolicy {
  public static bool UsesProxy(string command,string proxy){var match=Regex.Match(command??"",@"(?:^|\s)--proxy-server(?:=|\s+)(?:""(?<value>[^""]+)""|(?<value>[^\s""]+))(?=\s|$)",RegexOptions.IgnoreCase);return match.Success&&ProxyTools.Normalize(match.Groups["value"].Value)==proxy;}
  public static bool NeedsRestart(bool hasRoots,bool managed,bool sameProxy,bool manual,bool hasPlan){return hasRoots&&(manual||(managed&&(!sameProxy||hasPlan)));}
  public static bool FastStart(bool hasRoots,bool openRequested,bool autoStart,bool manual){return !hasRoots&&(openRequested||autoStart||manual);}
  public static bool IsCodexTarget(string path){return Regex.IsMatch(path??"",@"\\WindowsApps\\OpenAI\.Codex_[^\\]+\\app\\ChatGPT\.exe$",RegexOptions.IgnoreCase);}
 }
 public sealed class TrafficSnapshot {
  public string AppResponseUtc="",AppResponseNode="",AppResponseGroup="";public bool Valid;public double Up,Down,ServiceUp,ServiceDown;public Dictionary<string,string> CriticalActivityByNode=new Dictionary<string,string>();public int Connections,ServiceConnections;public bool ProcessAssociated;public string UpdatedUtc="",LastServiceActivityUtc="";
  public bool Quiet(DateTime now){return ServiceConnections>0&&!String.IsNullOrEmpty(LastServiceActivityUtc)&&(now-Files.Date(LastServiceActivityUtc)).TotalSeconds>=180;}
 }
 public sealed class TrafficTracker {
  bool baseline;double up,down;DateTime time;string lastActivity="",appResponse="",appNode="",appGroup="";Dictionary<string,string> criticalActivity=new Dictionary<string,string>();Dictionary<string,double> serviceUploads=new Dictionary<string,double>(),serviceDownloads=new Dictionary<string,double>();Dictionary<string,double> serviceBytes=new Dictionary<string,double>();
  public static bool ServiceHost(string host){string h=(host??"").TrimEnd('.').ToLowerInvariant();return new[]{"chatgpt.com","openai.com","oaistatic.com","oaiusercontent.com"}.Any(root=>h==root||h.EndsWith("."+root));}
  public TrafficSnapshot Sample(Dictionary<string,object> data,DateTime now){var snapshot=new TrafficSnapshot{UpdatedUtc=now.ToString("o")};double currentUp=NodePolicy.Num(data,"uploadTotal"),currentDown=NodePolicy.Num(data,"downloadTotal");double seconds=(now-time).TotalSeconds;
   if(baseline&&seconds>0&&currentUp>=up&&currentDown>=down){snapshot.Valid=true;snapshot.Up=(currentUp-up)/seconds;snapshot.Down=(currentDown-down)/seconds;}
   double serviceUp=0,serviceDown=0;var nextUploads=new Dictionary<string,double>();var nextDownloads=new Dictionary<string,double>();var next=new Dictionary<string,double>();foreach(object raw in NodePolicy.Array(data,"connections")){var c=raw as Dictionary<string,object>;if(c==null)continue;snapshot.Connections++;var meta=NodePolicy.Obj(c,"metadata");if(!ServiceHost(NodePolicy.Str(meta,"host")))continue;
    snapshot.ServiceConnections++;string process=NodePolicy.Str(meta,"process"),path=NodePolicy.Str(meta,"processPath");if(process.Equals("ChatGPT.exe",StringComparison.OrdinalIgnoreCase)||process.Equals("codex.exe",StringComparison.OrdinalIgnoreCase)||LaunchPolicy.IsCodexTarget(path))snapshot.ProcessAssociated=true;
    string id=NodePolicy.Str(c,"id");double bytes=NodePolicy.Num(c,"upload")+NodePolicy.Num(c,"download"),previous;if(id.Length>0){next[id]=bytes;double uploaded=NodePolicy.Num(c,"upload"),downloaded=NodePolicy.Num(c,"download"),old;nextUploads[id]=uploaded;nextDownloads[id]=downloaded;if(serviceUploads.TryGetValue(id,out old)&&uploaded>=old)serviceUp+=uploaded-old;if(serviceDownloads.TryGetValue(id,out old)&&downloaded>=old)serviceDown+=downloaded-old;
     if(serviceBytes.TryGetValue(id,out previous)&&bytes>previous){lastActivity=now.ToString("o");string host=NodePolicy.Str(meta,"host"),kind=NodePolicy.Str(meta,"type");DateTime started=Files.Date(NodePolicy.Str(c,"start"));bool longConnection=started!=DateTime.MinValue&&(now-started).TotalSeconds>=15;bool appSource=process.Equals("ChatGPT.exe",StringComparison.OrdinalIgnoreCase)||process.Equals("codex.exe",StringComparison.OrdinalIgnoreCase)||LaunchPolicy.IsCodexTarget(path)||(String.IsNullOrEmpty(process)&&String.IsNullOrEmpty(path)&&NodePolicy.Num(meta,"sourcePort")>0);bool critical=new[]{"ws.chatgpt.com","codex-cloud-environments.chatgpt.com","chatgpt.com","chat.openai.com","api.openai.com"}.Contains(host);var chain=NodePolicy.Array(c,"chains");if(kind!="Inner"&&critical&&chain.Length>0&&serviceDownloads.TryGetValue(id,out old)&&downloaded>old){if(longConnection&&appSource)criticalActivity[Convert.ToString(chain[0])]=lastActivity;bool knownApp=process.Equals("ChatGPT.exe",StringComparison.OrdinalIgnoreCase)||process.Equals("codex.exe",StringComparison.OrdinalIgnoreCase)||LaunchPolicy.IsCodexTarget(path);if(knownApp){appResponse=lastActivity;appNode=Convert.ToString(chain[0]);appGroup=chain.Length>1?Convert.ToString(chain[chain.Length-1]):"";}}}}

   }
   serviceBytes=next;serviceUploads=nextUploads;serviceDownloads=nextDownloads;if(snapshot.Valid&&seconds>0){snapshot.ServiceUp=serviceUp/seconds;snapshot.ServiceDown=serviceDown/seconds;}snapshot.CriticalActivityByNode=criticalActivity.Where(x=>(now-Files.Date(x.Value)).TotalSeconds<30).ToDictionary(x=>x.Key,x=>x.Value);snapshot.AppResponseUtc=appResponse;snapshot.AppResponseNode=appNode;snapshot.AppResponseGroup=appGroup;snapshot.LastServiceActivityUtc=lastActivity;baseline=true;up=currentUp;down=currentDown;time=now;return snapshot;
  }
  public void Reset(){baseline=false;serviceBytes.Clear();serviceUploads.Clear();serviceDownloads.Clear();criticalActivity.Clear();lastActivity=appResponse=appNode=appGroup="";}
 }
 public sealed class NodeRecord {
  public int Successes {get;set;}public int Failures {get;set;}public int LastDelay {get;set;}public string LastFailureUtc {get;set;}
 }
 public static class Reliability {
  public static double Rank(NodeRecord record,DateTime now){if(record==null)return 0;if((now-Files.Date(record.LastFailureUtc)).TotalMinutes<5)return -1000;return 100.0*(record.Successes+1)/(record.Successes+record.Failures+2)-Math.Min(20,record.LastDelay/100.0);}
  public static void Observe(Dictionary<string,NodeRecord> history,string key,bool ok,int delay,DateTime now){NodeRecord record;if(!history.TryGetValue(key,out record)){record=new NodeRecord();history[key]=record;}if(ok){record.Successes=Math.Min(1000,record.Successes+1);record.LastDelay=delay;}else{record.Failures=Math.Min(1000,record.Failures+1);record.LastFailureUtc=now.ToString("o");}}
 }
}
