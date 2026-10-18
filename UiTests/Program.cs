using System.Reflection;
using System.Runtime.InteropServices;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using KuzushiBeacon;

internal static unsafe class Program
{
 static T Stub<T>() where T:class=>DispatchProxy.Create<T,UiStub>();
 static void Main(string[] args)
 {
  var output=Path.GetFullPath(args[0]);Directory.CreateDirectory(output);UiStub.Directory=Path.Combine(output,"config");
  ImGui.CreateContext();var io=ImGui.GetIO();io.DisplaySize=new(1440,900);io.DeltaTime=1f/60;io.ConfigInputTrickleEventQueue=false;
  using(var plugin=new Plugin(Stub<IDalamudPluginInterface>(),Stub<IClientState>(),Stub<IObjectTable>(),Stub<IFramework>(),Stub<IGameGui>(),Stub<ICommandManager>(),Stub<IPluginLog>(),Stub<ITargetManager>(),Stub<IChatGui>(),Stub<IPlayerState>(),Stub<IDutyState>(),Stub<IDataManager>(),Stub<IGameInteropProvider>()))
  {
   var type=typeof(Plugin);
   object? Call(string name,params object[] values)=>type.GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(plugin,values);
   Call("Open");io.Fonts.Build();
   var glyphs=(string)type.Assembly.GetType("KuzushiBeacon.SupportGlyphs")!.GetField("Text")!.GetRawConstantValue()!;
   foreach(var f in UiStub.Fonts)foreach(var ch in glyphs.Distinct())
    if(f.FindGlyphNoFallback(ch)==null)throw new Exception("Missing support glyph U+"+((int)ch).ToString("X4"));
   var textures=new Dictionary<ulong,(nint,int,int)>();
   for(int i=0;i<io.Fonts.Textures.Size;i++)
   {byte* pixels;int w=0,h=0,bpp=0;io.Fonts.GetTexDataAsRGBA32(i,&pixels,&w,&h,&bpp);io.Fonts.SetTexID(i,new ImTextureID((ulong)i+1));textures[(ulong)i+1]=((nint)pixels,w,h);}
   int frames=0;
   foreach(var language in new[]{"ja","en"})
   {
    UiStub.Config.Language=language;
    for(int g=0;g<8;g++)
    {
     for(int f=0;f<2;f++)
     {
      ImGui.NewFrame();ImGui.SetNextWindowPos(new(30,30));ImGui.SetNextWindowSize(new(1000,840));ImGui.Begin("PvP Supporter - "+FeatureCatalog.Group(g,language));
      ImGui.PushFont(UiStub.Fonts[0]);Call("DrawFeatureGroup",g);ImGui.PopFont();ImGui.End();ImGui.Render();frames++;
     }
     if(ImGui.GetDrawData().TotalVtxCount==0)throw new Exception("Empty category");
     Renderer.Render(ImGui.GetDrawData(),textures,Path.Combine(output,$"category-{g}-{language}.bmp"));
    }
   }
   for(int i=0;i<10;i++){ImGui.NewFrame();Call("DrawSettingsWindow");ImGui.Render();}
   Renderer.Render(ImGui.GetDrawData(),textures,Path.Combine(output,"settings-en.bmp"));
   foreach(var language in new[]{"ja","en"})
   {
    UiStub.Config.Language=language;
    for(int page=0;page<10;page++)
    {
     type.GetField("selectedSettingsPage",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(plugin,page);
     for(int f=0;f<2;f++){ImGui.NewFrame();Call("DrawSettingsWindow");ImGui.Render();frames++;}
     if(ImGui.GetDrawData().TotalVtxCount==0)throw new Exception("Empty navigation page");
     if(page is 2 or 9)Renderer.Render(ImGui.GetDrawData(),textures,Path.Combine(output,"page-"+page+"-"+language+".bmp"));
    }
   }
   UiStub.Pvp=true;UiStub.Config.Language="ja";
   UiStub.Config.Support.Features=FeatureCatalog.All.Where(f=>f.Id!=78&&f.Id!=99).Select(f=>f.Id).ToHashSet();
   void Set(string field,object value)=>type.GetField(field,BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(plugin,value);
   var actors=(List<Observation>)type.GetField("observations",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(plugin)!;
   actors.Add(new(new(2,2,2,new(5,0,0),true,true),5000,50000,.5f,true,false,1,4,0,false,false,-1000,false));
   actors.Add(new(new(3,3,3,new(9,0,0),true,true),10000,50000,.5f,true,false,1,1,0,false,false,2000,true));
   actors.Add(new(new(4,4,4,new(-4,0,0),true,true),15000,50000,.5f,false,true,2,0,0,false,false,0,false));
   Set("currentObservation",actors[0]);Set("recommended",actors[0].Actor);Set("trackedAlly",4UL);Set("nearbyEnemies",2);Set("nearbyAllies",1);Set("guardReady",true);Set("chiten",3f);
   Set("supportAt",System.Diagnostics.Stopwatch.GetTimestamp()/(double)System.Diagnostics.Stopwatch.Frequency);
   for(int i=0;i<2;i++){ImGui.NewFrame();Call("DrawSupportOverlay");ImGui.Render();}
   Renderer.Render(ImGui.GetDrawData(),textures,Path.Combine(output,"support-hud-ja.bmp"));
   UiStub.Pvp=false;
   if(UiStub.Saves!=0)throw new Exception("Unchanged UI must not save");
   Console.WriteLine($"PASS {frames+12} headless production UI frames; 8 categories and 10 sidebar pages in Japanese and English; zero unchanged config writes.");
  }
  ImGui.DestroyContext();foreach(var pin in UiStub.Pins)pin.Free();
 }
}
