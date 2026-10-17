using System.Numerics;
using Dalamud.Configuration;
using Dalamud.Game;

namespace KuzushiBeacon;

public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public string? Language { get; set; }
    public bool Enabled = true;
    public bool ShowLine = true;
    public bool ShowMarker = true;
    public Vector4 LineColor = new(1f, 0.32f, 0.12f, 0.85f);
    public Vector4 MarkerColor = new(1f, 0.75f, 0.12f, 1f);
    public float LineWidth = 1.5f;
    public float MarkerSize = 12f;
    public float MarkerHeight = 2.5f;
    public float MaxDistance = 50f;
    public int ScanIntervalMs = 100;

    public bool InitializeLanguage(ClientLanguage? game, string? ui)
    {
        var saved = Loc.Normalize(Language);
        var resolved = saved ?? Loc.FromGame(game) ?? Loc.Normalize(ui) ?? "en";
        if (resolved == Language) return false;
        Language = resolved;
        return true;
    }

    public void Sanitize()
    {
        LineWidth = Clamp(LineWidth, 0.5f, 5f, 1.5f);
        MarkerSize = Clamp(MarkerSize, 5f, 30f, 12f);
        MarkerHeight = Clamp(MarkerHeight, 1f, 5f, 2.5f);
        MaxDistance = Clamp(MaxDistance, 5f, 100f, 50f);
        ScanIntervalMs = Math.Clamp(ScanIntervalMs, 50, 500);
        LineColor = Color(LineColor);
        MarkerColor = Color(MarkerColor);
    }

    private static float Clamp(float x, float min, float max, float fallback)
        => float.IsFinite(x) ? Math.Clamp(x, min, max) : fallback;
    private static Vector4 Color(Vector4 x) => new(Clamp(x.X, 0, 1, 1), Clamp(x.Y, 0, 1, 1),
        Clamp(x.Z, 0, 1, 1), Clamp(x.W, 0, 1, 1));
}
