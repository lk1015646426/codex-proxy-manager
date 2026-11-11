using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CodexProxyDesktop {
 public enum ProbeVerdict { Unknown,Healthy,ConfirmedFailed }
 public sealed class CountryEvidence {
  public string Name;public ProbeVerdict Verdict;public int Delay;public DateTime CheckedUtc;
 }
 public sealed class CountryAudit {
  public string Country;public string[] Members;public Dictionary<string,CountryEvidence> Evidence=new Dictionary<string,CountryEvidence>();
  public bool AllFailed(DateTime now){return Members!=null&&Members.Length>0&&Members.All(name=>Evidence.ContainsKey(name)&&Evidence[name].Verdict==ProbeVerdict.ConfirmedFailed&&(now-Evidence[name].CheckedUtc).TotalSeconds<=60);}
  public bool SameMembers(IEnumerable<string> names){return Members.OrderBy(n=>n).SequenceEqual(names.OrderBy(n=>n));}
 }
 public sealed class ServiceSample {
  public bool First,Second,RecentActivity,Uncertain;public int Delay,FirstResult=-3,SecondResult=-3;
  public bool Healthy {get{return First&&Second;}}
  public bool Failed {get{return !First&&!Second&&!RecentActivity&&!Uncertain;}}
 }
 public static class CountryFallback {
  public static bool Supported(string country){return country=="JP"||country=="SG";}
  public static string Other(string country){return country=="JP"?"SG":country=="SG"?"JP":"";}
  public static CountryAudit Scan(string country,NodeInfo[] nodes,Func<NodeInfo,ServiceSample> probe,Func<bool> canContinue,DateTime deadline,Action<CountryAudit> progress=null,Func<NodeInfo,ServiceSample> confirm=null){var result=new CountryAudit{Country=country,Members=nodes.Select(n=>n.Name).Distinct().ToArray()};var gate=new object();
   Parallel.ForEach(nodes,new ParallelOptions{MaxDegreeOfParallelism=3},node=>{var evidence=new CountryEvidence{Name=node.Name,Verdict=ProbeVerdict.Unknown,Delay=-1,CheckedUtc=DateTime.UtcNow};
    try{if(canContinue()&&DateTime.UtcNow<deadline){var first=probe(node);evidence.Delay=first.Delay;if(first.Healthy)evidence.Verdict=ProbeVerdict.Healthy;else if(first.Failed&&canContinue()&&DateTime.UtcNow<deadline){var second=(confirm??probe)(node);evidence.Delay=second.Delay;evidence.Verdict=second.Healthy?ProbeVerdict.Healthy:second.Failed?ProbeVerdict.ConfirmedFailed:ProbeVerdict.Unknown;}}
     if(!canContinue()||DateTime.UtcNow>=deadline)evidence.Verdict=ProbeVerdict.Unknown;
    }catch{evidence.Verdict=ProbeVerdict.Unknown;}evidence.CheckedUtc=DateTime.UtcNow;CountryAudit snapshot;lock(gate){result.Evidence[node.Name]=evidence;snapshot=new CountryAudit{Country=country,Members=result.Members,Evidence=new Dictionary<string,CountryEvidence>(result.Evidence)};}if(progress!=null)progress(snapshot);
   });return result;
  }
 }
}
