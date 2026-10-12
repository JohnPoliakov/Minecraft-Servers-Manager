using System.Text.RegularExpressions;

namespace Minecraft_Server_Manager.Services;

public static class JavaArguments
{
    public static string ApplyG1Preset(string current, int defaultRam)
    {
        // Keep memory, system properties and diagnostics. Only replace competing tuning options.
        var replaced = new HashSet<string>(StringComparer.Ordinal)
        {
            "UseG1GC", "UseZGC", "UseParallelGC", "UseSerialGC", "UseShenandoahGC", "UseConcMarkSweepGC",
            "ParallelRefProcEnabled", "MaxGCPauseMillis", "DisableExplicitGC", "AlwaysPreTouch",
            "G1NewSizePercent", "G1MaxNewSizePercent", "G1HeapRegionSize", "G1ReservePercent", "G1HeapWastePercent",
            "G1MixedGCCountTarget", "InitiatingHeapOccupancyPercent", "G1MixedGCLiveThresholdPercent",
            "G1RSetUpdatingPauseTimePercent", "SurvivorRatio", "PerfDisableSharedMem", "MaxTenuringThreshold"
        };
        string kept = Regex.Replace(current, @"(?<!\S)-XX:[+-]?(?<key>[^=\s]+)(?:=[^\s]+)?", match =>
            replaced.Contains(match.Groups["key"].Value) ? "" : match.Value).Trim();
        kept = Regex.Replace(kept, @"(?<!\S)-D(?:using\.aikars\.flags|aikars\.new\.flags)=\S+", "").Trim();
        if (!Regex.IsMatch(kept, @"(?<!\S)-Xmx\S+")) kept += $" -Xmx{Math.Max(1, defaultRam)}G";
        if (!Regex.IsMatch(kept, @"(?<!\S)-Xms\S+")) kept += " -Xms1G";
        return kept.Trim() + " -XX:+UseG1GC -XX:+ParallelRefProcEnabled -XX:MaxGCPauseMillis=200 -XX:+DisableExplicitGC";
    }
}
