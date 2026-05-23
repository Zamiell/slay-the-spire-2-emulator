namespace Sts2Emulator.Core;

public static class EnemyAI
{
    public static void ChooseIntents(List<EnemyState> enemies, int turn, Random rng)
    {
        foreach (var enemy in enemies.Where(e => e.Hp > 0))
            enemy.CurrentIntent = SelectIntent(enemy, rng);
    }

    public static void ExecuteIntent(EnemyState enemy, CombatState state, Random rng)
    {
        bool wasBuffMove = enemy.CurrentIntent.Type == IntentType.Buff;

        enemy.Block = 0; // block clears at start of enemy turn

        switch (enemy.CurrentIntent.Type)
        {
            case IntentType.Attack:
            {
                int damage = BuffSystem.IncomingDamage(
                    enemy.CurrentIntent.Magnitude,
                    enemy.Buffs,
                    state.PlayerBuffs
                );
                int absorbed = Math.Min(state.PlayerBlock, damage);
                state.PlayerBlock -= absorbed;
                state.PlayerHp = Math.Max(0, state.PlayerHp - (damage - absorbed));

                // FlameBarrier: retaliate with flat unpowered damage.
                int fb = BuffSystem.Get(state.PlayerBuffs, BuffId.FlameBarrier);
                if (fb > 0)
                {
                    int fbAbs = Math.Min(enemy.Block, fb);
                    enemy.Block -= fbAbs;
                    enemy.Hp = Math.Max(0, enemy.Hp - (fb - fbAbs));
                }
                break;
            }

            case IntentType.Defend:
                enemy.Block += BuffSystem.IncomingBlock(
                    enemy.CurrentIntent.Magnitude, enemy.Buffs);
                break;

            case IntentType.Buff:
                ApplyBuffIntent(enemy, state);
                break;
        }

        enemy.MoveIndex++;

        // Ritual: gain Strength at end of each enemy turn except the turn it was applied.
        if (!wasBuffMove)
        {
            int ritual = BuffSystem.Get(enemy.Buffs, BuffId.Ritual);
            if (ritual > 0)
                BuffSystem.Apply(enemy.Buffs, BuffId.Strength, ritual);
        }
    }

    // ── Per-enemy intent selection ─────────────────────────────────────────────

    private static Intent SelectIntent(EnemyState enemy, Random rng)
    {
        switch (enemy.DefId)
        {
            case KE.CalcifiedCultist:
                // Turn 0: Incantation (Buff). Turn 1+: Dark Strike (9 dmg, loops).
                return enemy.MoveIndex == 0
                    ? new Intent(IntentType.Buff, 0)
                    : new Intent(IntentType.Attack, 9);

            default:
                return GeneratedData.Enemies.ChooseIntent(enemy.DefId, enemy.MoveIndex, 0, rng);
        }
    }

    // ── Per-enemy buff actions ─────────────────────────────────────────────────

    private static void ApplyBuffIntent(EnemyState enemy, CombatState state)
    {
        switch (enemy.DefId)
        {
            case KE.CalcifiedCultist:
                // Incantation: apply 2 Ritual to self (gains +2 Strength each subsequent turn).
                BuffSystem.Apply(enemy.Buffs, BuffId.Ritual, 2);
                break;
        }
    }
}

// Known enemy def IDs (from Generated/Enemies.g.cs).
internal static class KE
{
    public const int CalcifiedCultist = 14;
}
