using System.Reflection;
using System.Runtime.InteropServices;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using KuzushiBeacon;

public unsafe class UiStub : DispatchProxy
{
    public static Configuration Config = new() { Language = "ja" };
    public static string Directory = "";
    public static int Saves, Removes;
    public static bool Pvp;
    public static List<ImFontPtr> Fonts = [];
    public static List<GCHandle> Pins = [];
    public ImFontPtr Font;
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        switch (method!.Name)
        {
            case "RunOnFrameworkThread": return Task.CompletedTask;
            case "get_IsLoggedIn": case "get_IsPvPExcludingDen": return Pvp;
            case "get_LocalPlayer": return Pvp?Create<Dalamud.Game.ClientState.Objects.SubKinds.IPlayerCharacter,UiStub>():null;
            case "IsValid": return true;
            case "get_Position": return new System.Numerics.Vector3(0,0,0);
            case "get_GameObjectId": return 1UL;
            case "get_EntityId": return 1U;
            case "get_CurrentHp": return 10000U;
            case "get_MaxHp": return 50000U;
            case "get_CurrentMp": return 4000U;
            case "get_HitboxRadius": return .5f;
            case "WorldToScreen":
                var pos=(System.Numerics.Vector3)args![0]!;args[1]=new System.Numerics.Vector2(750+pos.X*20,500-pos.Y*20+pos.Z*10);if(args.Length>2)args[2]=true;return true;
            case "GetPluginConfig": return Config;
            case "GetPluginConfigDirectory": return Directory;
            case "SavePluginConfig": Saves++; return null;
            case "AddHandler": return true;
            case "RemoveHandler": Removes++; return true;
            case "get_UiBuilder": case "get_FontAtlas": return Create(method.ReturnType, typeof(UiStub));
            case "get_BuildStep": return FontAtlasBuildStep.PreBuild;
            case "NewDelegateFontHandle":
                var tk = Create<IFontAtlasBuildToolkitPreBuild, UiStub>(); ((FontAtlasBuildStepDelegate)args![0]!)(tk);
                var handle = Create<IFontHandle, UiStub>(); ((UiStub)(object)handle).Font = Fonts.Last(); return handle;
            case "AddDalamudAssetFont":
                var safe = (SafeFontConfig)args![1]!;
                var pin = GCHandle.Alloc(safe.GlyphRanges, GCHandleType.Pinned); Pins.Add(pin);
                var native = ImGui.ImFontConfig(); native.FontDataOwnedByAtlas = true; native.OversampleH = 2; native.OversampleV = 1;
                native.RasterizerMultiply = 1; native.GlyphMaxAdvanceX = float.MaxValue; native.FontNo = safe.FontNo;
                var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncher/dalamudAssets/dev/UIRes/NotoSansCJK-Regular.ttc");
                var font = ImGui.GetIO().Fonts.AddFontFromFileTTF(path, safe.SizePx, native, (ushort*)pin.AddrOfPinnedObject()); native.Destroy(); Fonts.Add(font); return font;
            case "Push": ImGui.PushFont(Font); return new Pop();
        }
        return method.ReturnType == typeof(void) ? null : method.ReturnType.IsValueType ? Activator.CreateInstance(method.ReturnType) : null;
    }
    private sealed class Pop : IDisposable { public void Dispose() => ImGui.PopFont(); }
}
