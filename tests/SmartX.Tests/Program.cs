using System.Diagnostics;
using System.Text.Json;
using SmartX.Algorithms;
using SmartX.Models;
using SmartX.Services;
int passed = 0, failed = 0;
void Test(string name, Action run)
{
    try { run(); passed++; Console.WriteLine($"PASS  {name}"); }
    catch (Exception ex) { failed++; Console.WriteLine($"FAIL  {name}: {ex.Message}"); }
}
void Assert(bool value, string why = "Assertion failed") { if (!value) throw new Exception(why); }
void Throws<T>(Action action) where T : Exception
{ try { action(); } catch (T) { return; } throw new Exception($"Expected {typeof(T).Name}"); }
JsonElement State(Gateway g) => JsonSerializer.SerializeToElement(g.Dashboard());
Test("AVL balances ascending inserts and prunes narrow range", () => {
    var tree = new AvlTree<int,int>(); var watch = Stopwatch.StartNew();
    for (int i=0;i<100000;i++) tree.Insert(i,i);
    var r=tree.Range(50000,50010); watch.Stop();
    Assert(tree.IsBalanced() && tree.Height < 25 && tree.Count == 100000);
    Assert(r.Values.SequenceEqual(Enumerable.Range(50000,11)) && r.Visited < 60);
    Console.WriteLine($"      100,000 inserts + query: {watch.ElapsedMilliseconds} ms; height {tree.Height}; visited {r.Visited}.");
});
Test("AVL all four rotation patterns", () => {
    foreach(var order in new[]{new[]{3,2,1},new[]{1,2,3},new[]{3,1,2},new[]{1,3,2}})
    {var t=new AvlTree<int,int>();foreach(var k in order)t.Insert(k,k);Assert(t.IsBalanced()&&t.Height==2&&t.Range(1,3).Values.SequenceEqual(new[]{1,2,3}));}
});
Test("AVL random range matches ordered baseline", () => {
    var random = new Random(12);var t=new AvlTree<int,int>();var keys=Enumerable.Range(0,10000).OrderBy(_=>random.Next()).ToArray();
    foreach(var key in keys)t.Insert(key,key);
    for(int i=0;i<100;i++){int lo=random.Next(10000),hi=lo+random.Next(100);Assert(t.Range(lo,hi).Values.SequenceEqual(keys.Where(k=>k>=lo&&k<=hi).Order()));}
    Assert(t.IsBalanced());
});
Test("AVL same key updates rather than duplicates", () => {var t=new AvlTree<int,string>();t.Insert(1,"old");t.Insert(1,"new");Assert(t.Count==1&&t.Range(1,1).Values.Single()=="new");});
Test("AVL empty and outside ranges", () => {var t=new AvlTree<int,int>();Assert(t.Range(1,5).Values.Count==0);t.Insert(10,10);Assert(t.Range(1,5).Values.Count==0);});
Test("Recursive facility lookup reaches grandchildren", () => {Assert(Gateway.Locations.Contains("Sub-Zone B"));Assert(!Gateway.Locations.Contains("Unknown"));});
Test("Part 1 overloaded sensor operators", () => {var a=new SensorData("a",2);var b=new SensorData("b",5);Assert((a+b).Value==7&&b>a&&a<b);});
Test("Registration validates location and duplicate MAC", () => {
    var g=new Gateway();Throws<ArgumentException>(()=>g.Register(new("NEW","AA:BB:CC:00:00:01","New","Moisture","Lab")));
    Throws<ArgumentException>(()=>g.Register(new("NEW","AA:BB:CC:00:00:07","New","Moisture","Outside")));
    Assert(g.Register(new("NEW","AA:BB:CC:00:00:07","New","Moisture","Lab")).Id=="NEW");
});
Test("Critical queue pre-empts FIFO; standard preserves order", () => {
    var g=new Gateway();var a=g.Enqueue(new("TP-01",26));var b=g.Enqueue(new("PW-01",220));var c=g.Enqueue(new("MS-01",10));
    g.Process(1);var audit=State(g).GetProperty("audit")[0];Assert(audit.GetProperty("Message").GetString()!.Contains($"#{c.Sequence}:"));
    g.Process(1);Assert(State(g).GetProperty("audit")[0].GetProperty("Message").GetString()!.Contains($"#{a.Sequence}:"));
    g.Process(1);Assert(State(g).GetProperty("audit")[0].GetProperty("Message").GetString()!.Contains($"#{b.Sequence}:"));
});
Test("Severity and FIFO tie-breaking within critical heap", () => {
    var g=new Gateway();var a=g.Enqueue(new("MS-01",12));var b=g.Enqueue(new("PW-01",300));var c=g.Enqueue(new("PW-01",301));
    foreach(var expected in new[]{b,c,a}){g.Process(1);Assert(State(g).GetProperty("audit")[0].GetProperty("Message").GetString()!.Contains($"#{expected.Sequence}:"));}
});
Test("Paused processing retains packets", () => {var g=new Gateway();g.SetPaused(true);g.Enqueue(new("MS-01",12));Assert(g.Process()==0);g.SetPaused(false);Assert(g.Process()==1);});
Test("Telemetry rejects unknown, nonfinite and invalid moisture", () => {var g=new Gateway();Throws<KeyNotFoundException>(()=>g.Enqueue(new("NO",3)));Throws<ArgumentException>(()=>g.Enqueue(new("MS-01",double.NaN)));Throws<ArgumentException>(()=>g.Enqueue(new("MS-01",101)));});
Test("History preserves duplicate timestamps with sequence keys", () => {var g=new Gateway();var now=DateTimeOffset.UtcNow;g.Enqueue(new("MS-01",30,now));g.Enqueue(new("MS-01",31,now));g.Process();var h=g.History("MS-01",now,now);Assert(h.TotalMatches==2&&h.Readings[0].Value==30&&h.Readings[1].Value==31);});
Test("Backdated packets do not regress live value", () => {var g=new Gateway();g.Enqueue(new("MS-01",50));g.Process();g.Enqueue(new("MS-01",5,DateTimeOffset.UtcNow.AddMinutes(-10)));g.Process();Assert(g.Devices().Single(d=>d.Id=="MS-01").Value==50);Assert(!State(g).GetProperty("alerts").EnumerateArray().Any(a=>a.GetString()=="threshold:MS-01"));});
Test("History rejects reversed range", () => {var g=new Gateway();Throws<ArgumentException>(()=>g.History("MS-01",DateTimeOffset.UtcNow,DateTimeOffset.UtcNow.AddDays(-1)));});
Test("Undo restores two prior states in LIFO order", () => {var g=new Gateway();g.Execute(new("PM-01",true));g.Execute(new("PM-01",false));g.Undo();Assert(g.Devices().Single(d=>d.Id=="PM-01").Enabled);g.Undo();Assert(!g.Devices().Single(d=>d.Id=="PM-01").Enabled);Throws<ArgumentException>(()=>g.Undo());});
Test("Offline actuator blocks command and preserves undo", () => {var g=new Gateway();g.Execute(new("PM-01",true));g.SetOnline("PM-01",false);Throws<ArgumentException>(()=>g.Undo());Throws<ArgumentException>(()=>g.Execute(new("PM-01",false)));g.SetOnline("PM-01",true);g.Undo();Assert(!g.Devices().Single(d=>d.Id=="PM-01").Enabled);});
Test("HashSet suppresses duplicate disconnect events", () => {var g=new Gateway();g.SetOnline("MS-01",false);g.SetOnline("MS-01",false);Assert(State(g).GetProperty("audit").EnumerateArray().Count(a=>a.GetProperty("Kind").GetString()=="Disconnect")==1);g.SetOnline("MS-01",true);Assert(State(g).GetProperty("alerts").GetArrayLength()==0);});
Test("Threshold alert clears after healthy reading", () => {var g=new Gateway();g.Enqueue(new("MS-01",12));g.Process();Assert(State(g).GetProperty("alerts").GetArrayLength()==1);g.Enqueue(new("MS-01",50));g.Process();Assert(State(g).GetProperty("alerts").GetArrayLength()==0);});
Test("Patterns learned from actual searches suggest an actuator", () => {var g=new Gateway();g.Enqueue(new("MS-01",12));g.Process();g.RecordSearch("Pump A");g.RecordSearch("Pump A");Assert(State(g).GetProperty("insights").EnumerateArray().Any(i=>i.GetProperty("TargetId").GetString()=="PM-01"));g.Execute(new("PM-01",true));Assert(!State(g).GetProperty("insights").EnumerateArray().Any(i=>i.GetProperty("TargetId").GetString()=="PM-01"));});
Test("Patterns also learn repeated manual commands", () => {var g=new Gateway();g.Enqueue(new("MS-01",12));g.Process();g.Execute(new("PM-01",true));g.Undo();g.Execute(new("PM-01",true));g.Undo();Assert(State(g).GetProperty("insights").EnumerateArray().Any(i=>i.GetProperty("TargetId").GetString()=="PM-01"));});
Test("Trend predicts threshold before it is crossed", () => {var g=new Gateway();for(int i=0;i<4;i++){g.Enqueue(new("MS-01",35-i*4,DateTimeOffset.UtcNow.AddSeconds(-4+i)));g.Process();}Assert(State(g).GetProperty("insights").EnumerateArray().Any(i=>i.GetProperty("Title").GetString()=="Moisture trending down"));});
Test("Known mesh MST cost and shortest route", () => {var m=new Gateway().Mesh("MS-01","GW-01");Assert(m.Forest.Length==5&&m.ForestCost==9&&m.Components==1);Assert(m.Route.SequenceEqual(new[]{"MS-01","TP-01","GW-01"})&&m.RouteCost==3&&m.Reachable.Length==6);});
Test("Mesh reroutes after node failure", () => {var g=new Gateway();g.SetOnline("TP-01",false);var m=g.Mesh("MS-01","GW-01");Assert(!m.Route.Contains("TP-01")&&m.RouteCost==9&&m.Forest.Length==4);});
Test("Disconnected graph returns forest and unreachable nodes", () => {var g=new Gateway();g.SetOnline("TP-01",false);g.SetOnline("PW-01",false);var m=g.Mesh("MS-01","GW-01");Assert(m.Route.Length==0&&m.RouteCost==null&&m.Components==2&&m.Reachable.SequenceEqual(new[]{"GW-01"}));});
Test("Offline source and identical source/destination", () => {var g=new Gateway();Assert(g.Mesh("MS-01","MS-01").RouteCost==0);g.SetOnline("MS-01",false);Assert(g.Mesh("MS-01","GW-01").Route.Length==0);});
Test("Mesh link editing updates minimum route", () => {var g=new Gateway();g.UpsertLink(new("MS-01","GW-01",0.5));Assert(g.Mesh("MS-01","GW-01").RouteCost==0.5);g.RemoveLink("GW-01","MS-01");Assert(g.Mesh("MS-01","GW-01").RouteCost==3);Throws<ArgumentException>(()=>g.UpsertLink(new("MS-01","GW-01",-1)));});
Test("Attachment roundtrip and invalid format", () => {var g=new Gateway();var a=g.AddAttachment("MS-01","test.txt","text/plain",[65,66,67]);Assert(Convert.FromBase64String(g.GetAttachment(a.Id).Base64).SequenceEqual(new byte[]{65,66,67}));Throws<ArgumentException>(()=>g.AddAttachment("MS-01","run.exe","x",[1]));});
Test("Persistence restores telemetry, undo, mesh and attachments", () => {
    var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString(),"state.json");try{var g=new Gateway();g.Enqueue(new("MS-01",13));g.Process();g.Execute(new("PM-01",true));var a=g.AddAttachment("MS-01","test.txt","text/plain",[1,2]);g.UpsertLink(new("MS-01","GW-01",0.5));g.Save(path);var restored=new Gateway(false);restored.Load(path);Assert(restored.Devices().Length==6&&restored.History("MS-01",DateTimeOffset.MinValue,DateTimeOffset.MaxValue).TotalMatches==31);Assert(restored.Mesh("MS-01","GW-01").RouteCost==0.5&&restored.GetAttachment(a.Id).Name=="test.txt");restored.Undo();Assert(!restored.Devices().Single(d=>d.Id=="PM-01").Enabled);}finally{Directory.Delete(Path.GetDirectoryName(path)!,true);}
});
Test("Corrupt persistence fails without overwriting file", () => {var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".json");try{File.WriteAllText(path,"broken");Throws<InvalidDataException>(()=>new Gateway().Load(path));Assert(File.ReadAllText(path)=="broken");}finally{File.Delete(path);}});
Test("Concurrent ingestion does not lose or duplicate packets", () => {var g=new Gateway();Parallel.For(0,1000,i=>g.Enqueue(new("TP-01",25)));g.Process(10000);var h=g.History("TP-01",DateTimeOffset.MinValue,DateTimeOffset.MaxValue);Assert(h.TotalMatches==1030&&h.Readings.Select(r=>r.Sequence).Distinct().Count()==1030);});
Console.WriteLine($"\n{passed} passed; {failed} failed.");
return failed == 0 ? 0 : 1;
