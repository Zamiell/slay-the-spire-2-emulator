// AUTO-GENERATED — do not edit. Re-run scripts/extract_data.py to update.
namespace Sts2Emulator.GeneratedData;

internal static class Enemies
{
    private static readonly EnemyDef[] _all = [];

    public static EnemyDef Get(int id) =>
        Array.Find(_all, e => e.Id == id) is { Id: > 0 } def
            ? def
            : throw new ArgumentException($"Unknown enemy id {id}");

    public static Intent ChooseIntent(int enemyId, int moveIndex, int turn, Random rng)
    {
        // Populated after extraction — returns unknown until then
        return new Intent(IntentType.Unknown, 0);
    }

    public static void ApplyBuffIntent(EnemyState enemy, CombatState state, Random rng)
    {
        // Populated after extraction
    }
}
