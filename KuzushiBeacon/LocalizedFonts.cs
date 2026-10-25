using Dalamud;
using Dalamud.Interface.ManagedFontAtlas;

namespace KuzushiBeacon;

internal sealed class LocalizedFonts : IDisposable
{
    private readonly IFontHandle[] fonts;
    public LocalizedFonts(IFontAtlas atlas)
    {
        var glyphs = (Loc.FontText + CommandFeedback.FontText + HistoryFilesView.FontText + FeatureCatalog.FontText + SupportGlyphs.Text + new string(Enumerable.Range(32, 95).Select(x => (char)x).ToArray())).ToGlyphRange();
        fonts = Enumerable.Range(0, 4).Select(face => atlas.NewDelegateFontHandle(step => step.OnPreBuild(tk =>
            tk.AddDalamudAssetFont(DalamudAsset.NotoSansCjkRegular,
                new SafeFontConfig { SizePx = 17, FontNo = face, GlyphRanges = glyphs })))).ToArray();
    }
    public IDisposable Push(string? code) => fonts[code switch { "zh-Hant" => 3, "zh-Hans" => 2, "ko" => 1, _ => 0 }].Push();
    public void Dispose() { foreach (var font in fonts) font.Dispose(); }
}
