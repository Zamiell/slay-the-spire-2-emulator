using System;
using System.Collections.Generic;
using System.ComponentModel;
using Godot;
using Godot.Bridge;
using Godot.NativeInterop;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Random;

namespace MegaCrit.Sts2.Core.Nodes.Screens.Bestiary;

[ScriptPath("res://src/Core/Nodes/Screens/Bestiary/NBestiaryLayoutDefault.cs")]
public class NBestiaryLayoutDefault : NBestiaryLayout
{
	public new class MethodName : NBestiaryLayout.MethodName
	{
		public static readonly StringName Create = "Create";

		public new static readonly StringName _Ready = "_Ready";

		public new static readonly StringName Cleanup = "Cleanup";
	}

	public new class PropertyName : NBestiaryLayout.PropertyName
	{
		public static readonly StringName _creature = "_creature";

		public static readonly StringName _creatureContainer = "_creatureContainer";
	}

	public new class SignalName : NBestiaryLayout.SignalName
	{
	}

	private static readonly string _scenePath = SceneHelper.GetScenePath("screens/bestiary/bestiary_layout_default");

	private NCreature? _creature;

	private Control _creatureContainer;

	public static NBestiaryLayoutDefault? Create()
	{
		return PreloadManager.Cache.GetScene(_scenePath).Instantiate<NBestiaryLayoutDefault>(PackedScene.GenEditState.Disabled);
	}

	public override void _Ready()
	{
		_creatureContainer = GetNode<Control>("%MonsterVisualsContainer");
	}

	public override void Cleanup()
	{
		_creature?.QueueFreeSafely();
		_creature = null;
	}

	public override List<BestiaryMonsterMove> Setup(BestiaryEntry entry, Tween tween)
	{
		MonsterModel monsterModel = entry.monsterModel.ToMutable();
		monsterModel.Rng = Rng.Chaotic;
		_creature?.QueueFreeSafely();
		monsterModel.SetUpForCombat();
		Creature entity = new Creature(monsterModel, CombatSide.Enemy, null)
		{
			CombatState = new NullCombatState()
		};
		_creature = NCreature.Create(entity);
		_creatureContainer.AddChildSafely(_creature);
		_creature.SetupForBestiary();
		_creature.Position = new Vector2(0f, _creature.Hitbox.Size.Y * 0.5f);
		_creature.Modulate = StsColors.transparentBlack;
		tween.TweenProperty(_creature, "modulate", Colors.White, 0.25);
		return monsterModel.GenerateBestiaryMoveList(_creature.Visuals);
	}

	public override IEnumerable<NCreature> GetCreatures()
	{
		if (_creature == null)
		{
			return Array.Empty<NCreature>();
		}
		return new global::_003C_003Ez__ReadOnlySingleElementList<NCreature>(_creature);
	}

	[EditorBrowsable(EditorBrowsableState.Never)]
	internal new static List<MethodInfo> GetGodotMethodList()
	{
		List<MethodInfo> list = new List<MethodInfo>(3);
		list.Add(new MethodInfo(MethodName.Create, new PropertyInfo(Variant.Type.Object, "", PropertyHint.None, "", PropertyUsageFlags.Default, new StringName("Control"), exported: false), MethodFlags.Normal | MethodFlags.Static, null, null));
		list.Add(new MethodInfo(MethodName._Ready, new PropertyInfo(Variant.Type.Nil, "", PropertyHint.None, "", PropertyUsageFlags.Default, exported: false), MethodFlags.Normal, null, null));
		list.Add(new MethodInfo(MethodName.Cleanup, new PropertyInfo(Variant.Type.Nil, "", PropertyHint.None, "", PropertyUsageFlags.Default, exported: false), MethodFlags.Normal, null, null));
		return list;
	}

	[EditorBrowsable(EditorBrowsableState.Never)]
	protected override bool InvokeGodotClassMethod(in godot_string_name method, NativeVariantPtrArgs args, out godot_variant ret)
	{
		if (method == MethodName.Create && args.Count == 0)
		{
			ret = VariantUtils.CreateFrom<NBestiaryLayoutDefault>(Create());
			return true;
		}
		if (method == MethodName._Ready && args.Count == 0)
		{
			_Ready();
			ret = default(godot_variant);
			return true;
		}
		if (method == MethodName.Cleanup && args.Count == 0)
		{
			Cleanup();
			ret = default(godot_variant);
			return true;
		}
		return base.InvokeGodotClassMethod(in method, args, out ret);
	}

	[EditorBrowsable(EditorBrowsableState.Never)]
	internal static bool InvokeGodotClassStaticMethod(in godot_string_name method, NativeVariantPtrArgs args, out godot_variant ret)
	{
		if (method == MethodName.Create && args.Count == 0)
		{
			ret = VariantUtils.CreateFrom<NBestiaryLayoutDefault>(Create());
			return true;
		}
		ret = default(godot_variant);
		return false;
	}

	[EditorBrowsable(EditorBrowsableState.Never)]
	protected override bool HasGodotClassMethod(in godot_string_name method)
	{
		if (method == MethodName.Create)
		{
			return true;
		}
		if (method == MethodName._Ready)
		{
			return true;
		}
		if (method == MethodName.Cleanup)
		{
			return true;
		}
		return base.HasGodotClassMethod(in method);
	}

	[EditorBrowsable(EditorBrowsableState.Never)]
	protected override bool SetGodotClassPropertyValue(in godot_string_name name, in godot_variant value)
	{
		if (name == PropertyName._creature)
		{
			_creature = VariantUtils.ConvertTo<NCreature>(in value);
			return true;
		}
		if (name == PropertyName._creatureContainer)
		{
			_creatureContainer = VariantUtils.ConvertTo<Control>(in value);
			return true;
		}
		return base.SetGodotClassPropertyValue(in name, in value);
	}

	[EditorBrowsable(EditorBrowsableState.Never)]
	protected override bool GetGodotClassPropertyValue(in godot_string_name name, out godot_variant value)
	{
		if (name == PropertyName._creature)
		{
			value = VariantUtils.CreateFrom(in _creature);
			return true;
		}
		if (name == PropertyName._creatureContainer)
		{
			value = VariantUtils.CreateFrom(in _creatureContainer);
			return true;
		}
		return base.GetGodotClassPropertyValue(in name, out value);
	}

	[EditorBrowsable(EditorBrowsableState.Never)]
	internal static List<PropertyInfo> GetGodotPropertyList()
	{
		List<PropertyInfo> list = new List<PropertyInfo>();
		list.Add(new PropertyInfo(Variant.Type.Object, PropertyName._creature, PropertyHint.None, "", PropertyUsageFlags.ScriptVariable, exported: false));
		list.Add(new PropertyInfo(Variant.Type.Object, PropertyName._creatureContainer, PropertyHint.None, "", PropertyUsageFlags.ScriptVariable, exported: false));
		return list;
	}

	[EditorBrowsable(EditorBrowsableState.Never)]
	protected override void SaveGodotObjectData(GodotSerializationInfo info)
	{
		base.SaveGodotObjectData(info);
		info.AddProperty(PropertyName._creature, Variant.From(in _creature));
		info.AddProperty(PropertyName._creatureContainer, Variant.From(in _creatureContainer));
	}

	[EditorBrowsable(EditorBrowsableState.Never)]
	protected override void RestoreGodotObjectData(GodotSerializationInfo info)
	{
		base.RestoreGodotObjectData(info);
		if (info.TryGetProperty(PropertyName._creature, out var value))
		{
			_creature = value.As<NCreature>();
		}
		if (info.TryGetProperty(PropertyName._creatureContainer, out var value2))
		{
			_creatureContainer = value2.As<Control>();
		}
	}
}
