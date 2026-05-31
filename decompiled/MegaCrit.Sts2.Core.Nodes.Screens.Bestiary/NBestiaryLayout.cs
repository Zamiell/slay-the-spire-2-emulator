using System.Collections.Generic;
using System.ComponentModel;
using Godot;
using Godot.Bridge;
using Godot.NativeInterop;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace MegaCrit.Sts2.Core.Nodes.Screens.Bestiary;

[ScriptPath("res://src/Core/Nodes/Screens/Bestiary/NBestiaryLayout.cs")]
public abstract class NBestiaryLayout : Control
{
    public new class MethodName : Control.MethodName
    {
        public static readonly StringName Cleanup = "Cleanup";
    }

    public new class PropertyName : Control.PropertyName { }

    public new class SignalName : Control.SignalName { }

    public abstract void Cleanup();

    public abstract List<BestiaryMonsterMove> Setup(BestiaryEntry entry, Tween tween);

    public abstract IEnumerable<NCreature> GetCreatures();

    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static List<MethodInfo> GetGodotMethodList()
    {
        List<MethodInfo> list = new List<MethodInfo>(1);
        list.Add(
            new MethodInfo(
                MethodName.Cleanup,
                new PropertyInfo(
                    Variant.Type.Nil,
                    "",
                    PropertyHint.None,
                    "",
                    PropertyUsageFlags.Default,
                    exported: false
                ),
                MethodFlags.Normal,
                null,
                null
            )
        );
        return list;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    protected override bool InvokeGodotClassMethod(
        in godot_string_name method,
        NativeVariantPtrArgs args,
        out godot_variant ret
    )
    {
        if (method == MethodName.Cleanup && args.Count == 0)
        {
            Cleanup();
            ret = default(godot_variant);
            return true;
        }
        return base.InvokeGodotClassMethod(in method, args, out ret);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    protected override bool HasGodotClassMethod(in godot_string_name method)
    {
        if (method == MethodName.Cleanup)
        {
            return true;
        }
        return base.HasGodotClassMethod(in method);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    protected override void SaveGodotObjectData(GodotSerializationInfo info)
    {
        base.SaveGodotObjectData(info);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    protected override void RestoreGodotObjectData(GodotSerializationInfo info)
    {
        base.RestoreGodotObjectData(info);
    }
}
