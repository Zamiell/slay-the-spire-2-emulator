namespace Sts2Emulator.Core;

public enum CardType { Attack, Skill, Power, Status, Curse }

public readonly record struct CardDef(
    int Id,
    string Name,
    int Cost,
    int BaseDamage,
    int BaseBlock,
    int UpgradeDamage,
    int UpgradeBlock,
    CardType Type,
    bool Ethereal = false,
    bool Exhaust = false
);

public readonly record struct CardInstance(int DefId, bool Upgraded);
