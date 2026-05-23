// AUTO-GENERATED — do not edit. Re-run scripts/extract_data.py to update.
namespace Sts2Emulator.GeneratedData;

internal static class Relics
{
    private static readonly RelicDef[] _all = [];

    public static RelicDef Get(int id) =>
        Array.Find(_all, r => r.Id == id) is { Id: > 0 } def
            ? def
            : throw new ArgumentException($"Unknown relic id {id}");
}
