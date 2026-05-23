namespace Sts2Emulator.Core;

public enum BuffId
{
    Strength,
    Dexterity,
    Vulnerable,
    Weak,
    Frail,
    Poison,
    Burn,
    Ritual,
    // populated further once decompiled data is available
}

public record struct BuffState(BuffId Id, int Magnitude);
