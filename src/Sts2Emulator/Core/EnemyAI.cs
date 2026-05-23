namespace Sts2Emulator.Core;

public static class EnemyAI
{
    public static void ChooseIntents(List<EnemyState> enemies, int turn, Random rng)
    {
        foreach (var enemy in enemies.Where(e => e.Hp > 0))
            enemy.CurrentIntent = GeneratedData.Enemies.ChooseIntent(enemy.DefId, enemy.MoveIndex, turn, rng);
    }

    public static void ExecuteIntent(EnemyState enemy, CombatState state, Random rng)
    {
        enemy.Block = 0;

        switch (enemy.CurrentIntent.Type)
        {
            case IntentType.Attack:
                int damage = BuffSystem.IncomingDamage(
                    enemy.CurrentIntent.Magnitude,
                    enemy.Buffs.ToList(),
                    state.PlayerBuffs
                );
                int absorbed = Math.Min(state.PlayerBlock, damage);
                state.PlayerBlock -= absorbed;
                state.PlayerHp -= damage - absorbed;
                break;

            case IntentType.Defend:
                enemy.Block += BuffSystem.IncomingBlock(enemy.CurrentIntent.Magnitude, enemy.Buffs.ToList());
                break;

            case IntentType.Buff:
                GeneratedData.Enemies.ApplyBuffIntent(enemy, state, rng);
                break;
        }

        enemy.MoveIndex++;
    }
}
