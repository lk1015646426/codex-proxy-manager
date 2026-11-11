using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace CodexProxyDesktop {
 public static class PausePolicy {
  public static bool Active(Settings c,DateTime now){return Files.Date(c.RecoveryPauseUntilUtc)>now;}
  public static int Remaining(Settings c,DateTime now){return Math.Max(0,(int)Math.Min(86400,(Files.Date(c.RecoveryPauseUntilUtc)-now).TotalSeconds));}
 }
 public sealed class RecoveryRecord {
  public string AtUtc {get;set;}public string From {get;set;}public string To {get;set;}public string CountryFrom {get;set;}public string CountryTo {get;set;}public string Mode {get;set;}public string Group {get;set;}public string Reason {get;set;}public string Result {get;set;}public int Seconds {get;set;}
 }
 public sealed class RecoveryProgress {
  public int Id,Done,Total,Healthy,Failed,Unknown;public bool Active;public string Phase="",Country="",StartedUtc="";
  public RecoveryProgress Copy(){return (RecoveryProgress)MemberwiseClone();}
  public string Text(DateTime now){int seconds=String.IsNullOrEmpty(StartedUtc)?0:Math.Max(0,(int)(now-Files.Date(StartedUtc)).TotalSeconds);return Phase+(Total>0?" · "+Done+"/"+Total+" · 可达 "+Healthy+" / 故障 "+Failed+" / 未确认 "+Unknown:"")+(Active?" · "+seconds+" 秒":"");}
 }
 public sealed class UserAlert {public int Id;public string Title,Text;}
 public sealed class AlertGate {
  readonly object gate=new object();readonly HashSet<string> active=new HashSet<string>();readonly Dictionary<string,DateTime> last=new Dictionary<string,DateTime>();
  public bool Allow(string key,DateTime now){lock(gate){DateTime previous;if(active.Contains(key)||last.TryGetValue(key,out previous)&&(now-previous).TotalMinutes<5)return false;active.Add(key);last[key]=now;return true;}}
  public void Resolve(string key){lock(gate)active.Remove(key);}
 }
 public static class Presentation {
  public static string When(string utc){DateTime t=Files.Date(utc);return t==DateTime.MinValue?"—":t.AddHours(8).ToString("MM-dd HH:mm:ss");}
  public static string Csv(string value){string text=value??"";if(text.TrimStart().Length>0&&"=+-@".Contains(text.TrimStart()[0]))text="'"+text;return "\""+text.Replace("\"","\"\"")+"\"";}
  public static string HistoryCsv(IEnumerable<RecoveryRecord> records){var rows=new List<string>{"时间,模式,策略组,旧节点,新节点,原因,结果,耗时秒"};rows.AddRange(records.Select(r=>String.Join(",",new[]{When(r.AtUtc),r.Mode,r.Group,r.From,r.To,r.Reason,r.Result,r.Seconds.ToString()}.Select(Csv))));return String.Join(Environment.NewLine,rows);}
 }
 public static class FloatGeometry {
  public static int Scale(Settings c){return c.FloatingScale==0?100:Math.Max(80,Math.Min(140,c.FloatingScale));}
  public static int Opacity(Settings c){return c.FloatingOpacity==0?96:Math.Max(40,Math.Min(100,c.FloatingOpacity));}
  public static Rectangle Place(Point desired,int scale,bool compact,Rectangle area){int width=Math.Min(area.Width,(int)Math.Round((compact?184:310)*scale/100.0));int height=Math.Min(area.Height,(int)Math.Round((compact?36:136)*scale/100.0));return new Rectangle(Math.Max(area.Left,Math.Min(desired.X,area.Right-width)),Math.Max(area.Top,Math.Min(desired.Y,area.Bottom-height)),Math.Max(1,width),Math.Max(1,height));}
  public static bool CoversScreen(Rectangle window,Rectangle screen){return window.Left<=screen.Left+2&&window.Top<=screen.Top+2&&window.Right>=screen.Right-2&&window.Bottom>=screen.Bottom-2;}
 }
}
