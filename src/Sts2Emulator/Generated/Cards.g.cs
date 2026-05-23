// AUTO-GENERATED — do not edit. Re-run scripts/extract_data.py to update.
namespace Sts2Emulator.GeneratedData;

internal static class Cards
{
    private static readonly CardDef[] _all = [];

    public static CardDef Get(int id) =>
        Array.Find(_all, c => c.Id == id) is { Id: > 0 } def
            ? def
            : throw new ArgumentException($"Unknown card id {id}");
}
