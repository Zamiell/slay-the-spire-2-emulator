using System;

namespace MegaCrit.Sts2.Core.Hooks;

[Flags]
public enum HpLossHookPhase
{
    None = 0,
    BeforeOsty = 1,
    AfterOsty = 2,
    All = 3,
}
