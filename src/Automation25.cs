using System;
using System.Security.Cryptography;
using System.Text;

namespace CodexProxyDesktop {
 public sealed class AutomationCase25 {
  public string Id="",StartedUtc="",EndedUtc="",UpdatedUtc="",Phase="后台监测",Reason="",Action="正常使用即可，后台自动检查",InitialNode="",InitialReason="",LatestNode="",Outcome="",HealthySinceUtc="",LastHealthyProbeUtc="",AttentionSinceUtc="",HealthyRoute="";
  public bool Active,NeedsAttention,Notified;public int HealthySamples,RecoveryAttempts,Signals;
  public AutomationCase25 Copy(){return (AutomationCase25)MemberwiseClone();}
  public string Text {get{return Active||Phase=="已暂停"?Phase+" · "+Action:"后台监测 · "+(Outcome.Length>0?"上次事件已结束，继续自动检查":"正常使用即可，后台自动检查");}}
 }
 public sealed class AutomationChange25 {public string Kind,Text;public AutomationCase25 Case;}
 // This tracks network evidence. It never concludes that a conversation completed.
 public sealed class Automation25 {
  readonly object gate=new object();AutomationCase25 current;DateTime firstSeen=DateTime.MinValue,missingSince=DateTime.MinValue;
  public Automation25(AutomationCase25 saved=null,bool resume=true){current=saved??new AutomationCase25();if(current.Active&&resume){current.Phase="等待重新确认";current.Action="已接续上次未结束事件，重新核对网络证据";current.HealthySinceUtc=current.LastHealthyProbeUtc=current.AttentionSinceUtc="";current.HealthySamples=0;}}
  static string RouteScope(NetworkView v){using(var hash=SHA256.Create())return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(v.Mode+"|"+v.RuleRoot+"|"+v.Group+"|"+v.Node)));}
  public AutomationCase25 View {get{lock(gate)return current.Copy();}}
  AutomationChange25 Change(string kind,string text){return new AutomationChange25{Kind=kind,Text=text,Case=current.Copy()};}
  public AutomationChange25 Signal(string reason,string node,DateTime now,bool recovery=false){lock(gate){bool created=!current.Active;if(created)current=new AutomationCase25{Id=Guid.NewGuid().ToString("N"),Active=true,StartedUtc=now.ToString("o"),Phase="发现异常",Reason=DiagnosticJournal.Safe(reason),InitialNode=DiagnosticJournal.Safe(node),InitialReason=DiagnosticJournal.Safe(reason),Action="已保存现场，正在自动复查"};current.LatestNode=DiagnosticJournal.Safe(node);current.UpdatedUtc=now.ToString("o");current.Signals++;if(recovery){current.RecoveryAttempts++;current.Phase="恢复后观察";current.Action="核对服务探测，保留已有连接；任务状态未核验";current.HealthySamples=0;current.HealthyRoute="";current.HealthySinceUtc=current.LastHealthyProbeUtc="";}return created?Change("incident-start","开始自动处理："+current.Reason):null;}}
  public AutomationChange25 Step(NetworkView v,Settings c,DateTime now){lock(gate){if(firstSeen==DateTime.MinValue)firstSeen=now;bool route=Evidence23.Fresh(v.RouteUtc,now,8)&&(v.Mode!="rule"||v.RuleVerified);bool probe=Evidence23.Fresh(v.ServiceProbeUtc,now,35);bool failure=Evidence23.Relevant(v.LinkFailure,v,now);bool response=Evidence23.AppResponse(v,now);bool healthy=v.Controller&&route&&probe&&v.ServiceKnown&&v.ChatGptReachable&&v.CodexReachable&&!failure&&!v.PendingRecovery;
    bool missing=!v.Controller||!route||!probe;if(missing){if(missingSince==DateTime.MinValue)missingSince=now;}else missingSince=DateTime.MinValue;
    bool partial=probe&&v.ServiceKnown&&(!v.ChatGptReachable||!v.CodexReachable);string reason=!v.Controller?"本地监控未连接":!route?"路线信息待确认":failure?"相关连接存在失败线索":partial?(v.ChatGptReachable||v.CodexReachable?"部分目标探测异常":"服务目标探测失败"):!probe?"探测信息待更新":"";
    if(!current.Active){if(c.Paused){current.Phase="已暂停";current.Action="按用户设置暂停，现有连接保留";return null;}current.Phase="后台监测";bool missingLong=missingSince!=DateTime.MinValue&&(now-missingSince).TotalSeconds>=20;if(!v.PendingRecovery&&!partial&&!failure&&!missingLong)return null;return Signal(v.PendingRecovery?"上次切换结果待核对":reason,v.Node,now);}
    string old=current.Phase+"|"+current.Reason+"|"+current.NeedsAttention;current.LatestNode=DiagnosticJournal.Safe(v.Node);current.UpdatedUtc=now.ToString("o");string phase,action;
    if(c.Paused){phase="已暂停";action="按用户设置暂停，现有连接保留";}
    else if(v.PendingRecovery){phase="需要核对";action="上次切换结果未确认，请在节点恢复页核对；已停止自动写入";}
    else if(v.Recovering){phase="正在恢复";action=String.IsNullOrEmpty(v.RecoveryPhase)?"正在按已保存策略检查候选节点":v.RecoveryPhase;}
    else if(healthy){phase="恢复后观察";action="服务探测已有响应，继续观察稳定性；任务状态未核验";string routeKey=RouteScope(v);if(current.HealthyRoute!=routeKey){current.HealthyRoute=routeKey;current.HealthySamples=0;current.HealthySinceUtc=current.LastHealthyProbeUtc="";}if(current.HealthySinceUtc.Length==0)current.HealthySinceUtc=now.ToString("o");if(current.LastHealthyProbeUtc!=v.ServiceProbeUtc){current.LastHealthyProbeUtc=v.ServiceProbeUtc;current.HealthySamples++;}if(current.HealthySamples>=2&&(now-Files.Date(current.HealthySinceUtc)).TotalSeconds>=15){current.Active=false;current.NeedsAttention=false;current.EndedUtc=now.ToString("o");current.Phase="事件已结束";current.Outcome="服务探测连续恢复，当前任务是否完成仍未核验";current.Action="无需操作，继续后台监测";return Change("incident-summary","故障处理结束："+current.Outcome+"；恢复操作 "+current.RecoveryAttempts+" 次");}}
    else if(response){phase="继续观察";action="已有应用连接有数据返回，保留当前连接并复查异常目标";}
    else if(!v.Controller){phase="等待监控重连";action="自动重新发现本机接口，保留应用会话";}
    else if(!route){phase="继续观察";action="继续核对路线；自动恢复受限，不修改未确认的规则";}
    else if(!String.IsNullOrEmpty(v.RecoveryWaitText)){phase="等待复查";action=v.RecoveryWaitText+"，到期自动复查";}
    else if(!c.AutoSwitch||PausePolicy.Active(c,now)){phase="继续观察";action="按已保存设置继续监测和复查，保留现有连接";}
    else{phase="自动复查";action="核对服务目标与恢复条件；证据不足时保留原选择";}
    if(!healthy||c.Paused||v.Recovering){current.HealthySamples=0;current.HealthyRoute="";current.HealthySinceUtc=current.LastHealthyProbeUtc="";}current.Phase=phase;if(reason.Length>0)current.Reason=reason;current.Action=DiagnosticJournal.Safe(action);
    bool severe=!c.Paused&&!healthy&&!response&&(!v.Controller||probe&&v.ServiceKnown&&!v.ChatGptReachable&&!v.CodexReachable);if(severe){if(current.AttentionSinceUtc.Length==0)current.AttentionSinceUtc=now.ToString("o");}else current.AttentionSinceUtc="";current.NeedsAttention=!c.Paused&&(v.PendingRecovery||severe&&(now-Files.Date(current.AttentionSinceUtc)).TotalSeconds>=60);
    if(current.NeedsAttention&&!v.PendingRecovery)current.Action+="；异常持续，可打开诊断查看处理依据";string next=current.Phase+"|"+current.Reason+"|"+current.NeedsAttention;return old==next?null:Change("incident-phase","自动处理进度："+current.Phase);}}
  public void MarkNotified(){lock(gate)current.Notified=true;}
  public AutomationChange25 Suspend(DateTime now){lock(gate){if(!current.Active)return null;current.UpdatedUtc=now.ToString("o");current.Phase="等待重新确认";current.Action="管理器已退出；事件保留，下次启动重新确认";current.HealthySinceUtc=current.LastHealthyProbeUtc=current.AttentionSinceUtc="";current.HealthySamples=0;return Change("incident-suspended","管理器退出，未证明故障恢复");}}
 }
}
