using System.Collections.Generic;
using System.ComponentModel;
using Godot;
using Godot.Bridge;
using Godot.NativeInterop;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;

namespace MegaCrit.Sts2.Core.Nodes.Screens.Bestiary;

[ScriptPath("res://src/Core/Nodes/Screens/Bestiary/NBestiaryMoveButton.cs")]
public class NBestiaryMoveButton : NButton
{
    public new class MethodName : NButton.MethodName
    {
        public static new readonly StringName _Ready = "_Ready";

        public static new readonly StringName _UnhandledInput = "_UnhandledInput";

        public static new readonly StringName OnRelease = "OnRelease";

        public static new readonly StringName OnPress = "OnPress";

        public static new readonly StringName OnFocus = "OnFocus";

        public static new readonly StringName OnUnfocus = "OnUnfocus";
    }

    public new class PropertyName : NButton.PropertyName
    {
        public static new readonly StringName ClickedSfx = "ClickedSfx";

        public static new readonly StringName HoveredSfx = "HoveredSfx";

        public static readonly StringName _buttonAnimator = "_buttonAnimator";

        public static readonly StringName _label = "_label";

        public static readonly StringName _tween = "_tween";

        public static readonly StringName _clickTween = "_clickTween";

        public static readonly StringName _hotkey = "_hotkey";
    }

    public new class SignalName : NButton.SignalName { }

    private static readonly string _scenePath = SceneHelper.GetScenePath(
        "screens/bestiary/bestiary_move_button"
    );

    private Control _buttonAnimator;

    private MegaLabel _label;

    private Tween? _tween;

    private Tween? _clickTween;

    private StringName _hotkey;

    protected override string? ClickedSfx => null;

    protected override string? HoveredSfx => null;

    public BestiaryMonsterMove Move { get; private set; }

    public override void _Ready()
    {
        ConnectSignals();
        _label = GetNode<MegaLabel>("%Label");
        _buttonAnimator = GetNode<Control>("%ButtonAnimator");
        _label.SetTextAutoSize(Move.displayName);
        _label.PivotOffset = new Vector2(0f, _label.Size.Y * 0.5f);
        _buttonAnimator.PivotOffset = new Vector2(0f, _buttonAnimator.Size.Y * 0.5f);
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if ((!(NControllerManager.Instance?.IsUsingController)) ?? true)
        {
            if (input.IsActionPressed(_hotkey))
            {
                OnPress();
            }
            else if (input.IsActionReleased(_hotkey))
            {
                EmitSignal(NClickableControl.SignalName.Released, this);
                OnRelease();
            }
        }
    }

    public static NBestiaryMoveButton Create(BestiaryMonsterMove move, StringName setHotkey)
    {
        NBestiaryMoveButton nBestiaryMoveButton = PreloadManager
            .Cache.GetAsset<PackedScene>(_scenePath)
            .Instantiate<NBestiaryMoveButton>(PackedScene.GenEditState.Disabled);
        nBestiaryMoveButton._hotkey = setHotkey;
        nBestiaryMoveButton.Move = move;
        return nBestiaryMoveButton;
    }

    protected override void OnRelease()
    {
        base.OnRelease();
        _label.Modulate = StsColors.green;
        _clickTween?.Kill();
        _clickTween = CreateTween().SetParallel();
        _clickTween
            .TweenProperty(_buttonAnimator, "scale", Vector2.One, 0.3)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Bounce);
        _clickTween.TweenProperty(_label, "modulate", StsColors.cream, 0.2);
    }

    protected override void OnPress()
    {
        base.OnPress();
        _clickTween?.Kill();
        _clickTween = CreateTween().SetParallel();
        _clickTween
            .TweenProperty(_buttonAnimator, "scale", Vector2.One * 0.9f, 0.2)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Expo);
        _clickTween.TweenProperty(_label, "modulate", StsColors.lightGray, 0.05);
    }

    protected override void OnFocus()
    {
        base.OnFocus();
        _tween?.Kill();
        _tween = CreateTween().SetParallel();
        _tween.TweenProperty(_label, "scale", Vector2.One * 1.05f, 0.05);
    }

    protected override void OnUnfocus()
    {
        _tween?.Kill();
        _tween = CreateTween().SetParallel();
        _tween
            .TweenProperty(_label, "scale", Vector2.One, 0.25)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Cubic);
        _clickTween?.Kill();
        _clickTween = CreateTween().SetParallel();
        _clickTween
            .TweenProperty(_buttonAnimator, "scale", Vector2.One, 0.1)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Expo);
        _clickTween.TweenProperty(_label, "modulate", StsColors.cream, 0.1);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static new List<MethodInfo> GetGodotMethodList()
    {
        List<MethodInfo> list = new List<MethodInfo>(6);
        list.Add(
            new MethodInfo(
                MethodName._Ready,
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
        list.Add(
            new MethodInfo(
                MethodName._UnhandledInput,
                new PropertyInfo(
                    Variant.Type.Nil,
                    "",
                    PropertyHint.None,
                    "",
                    PropertyUsageFlags.Default,
                    exported: false
                ),
                MethodFlags.Normal,
                new List<PropertyInfo>
                {
                    new PropertyInfo(
                        Variant.Type.Object,
                        "input",
                        PropertyHint.None,
                        "",
                        PropertyUsageFlags.Default,
                        new StringName("InputEvent"),
                        exported: false
                    ),
                },
                null
            )
        );
        list.Add(
            new MethodInfo(
                MethodName.OnRelease,
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
        list.Add(
            new MethodInfo(
                MethodName.OnPress,
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
        list.Add(
            new MethodInfo(
                MethodName.OnFocus,
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
        list.Add(
            new MethodInfo(
                MethodName.OnUnfocus,
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
        if (method == MethodName._Ready && args.Count == 0)
        {
            _Ready();
            ret = default(godot_variant);
            return true;
        }
        if (method == MethodName._UnhandledInput && args.Count == 1)
        {
            _UnhandledInput(VariantUtils.ConvertTo<InputEvent>(in args[0]));
            ret = default(godot_variant);
            return true;
        }
        if (method == MethodName.OnRelease && args.Count == 0)
        {
            OnRelease();
            ret = default(godot_variant);
            return true;
        }
        if (method == MethodName.OnPress && args.Count == 0)
        {
            OnPress();
            ret = default(godot_variant);
            return true;
        }
        if (method == MethodName.OnFocus && args.Count == 0)
        {
            OnFocus();
            ret = default(godot_variant);
            return true;
        }
        if (method == MethodName.OnUnfocus && args.Count == 0)
        {
            OnUnfocus();
            ret = default(godot_variant);
            return true;
        }
        return base.InvokeGodotClassMethod(in method, args, out ret);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    protected override bool HasGodotClassMethod(in godot_string_name method)
    {
        if (method == MethodName._Ready)
        {
            return true;
        }
        if (method == MethodName._UnhandledInput)
        {
            return true;
        }
        if (method == MethodName.OnRelease)
        {
            return true;
        }
        if (method == MethodName.OnPress)
        {
            return true;
        }
        if (method == MethodName.OnFocus)
        {
            return true;
        }
        if (method == MethodName.OnUnfocus)
        {
            return true;
        }
        return base.HasGodotClassMethod(in method);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    protected override bool SetGodotClassPropertyValue(
        in godot_string_name name,
        in godot_variant value
    )
    {
        if (name == PropertyName._buttonAnimator)
        {
            _buttonAnimator = VariantUtils.ConvertTo<Control>(in value);
            return true;
        }
        if (name == PropertyName._label)
        {
            _label = VariantUtils.ConvertTo<MegaLabel>(in value);
            return true;
        }
        if (name == PropertyName._tween)
        {
            _tween = VariantUtils.ConvertTo<Tween>(in value);
            return true;
        }
        if (name == PropertyName._clickTween)
        {
            _clickTween = VariantUtils.ConvertTo<Tween>(in value);
            return true;
        }
        if (name == PropertyName._hotkey)
        {
            _hotkey = VariantUtils.ConvertTo<StringName>(in value);
            return true;
        }
        return base.SetGodotClassPropertyValue(in name, in value);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    protected override bool GetGodotClassPropertyValue(
        in godot_string_name name,
        out godot_variant value
    )
    {
        string from;
        if (name == PropertyName.ClickedSfx)
        {
            from = ClickedSfx;
            value = VariantUtils.CreateFrom(in from);
            return true;
        }
        if (name == PropertyName.HoveredSfx)
        {
            from = HoveredSfx;
            value = VariantUtils.CreateFrom(in from);
            return true;
        }
        if (name == PropertyName._buttonAnimator)
        {
            value = VariantUtils.CreateFrom(in _buttonAnimator);
            return true;
        }
        if (name == PropertyName._label)
        {
            value = VariantUtils.CreateFrom(in _label);
            return true;
        }
        if (name == PropertyName._tween)
        {
            value = VariantUtils.CreateFrom(in _tween);
            return true;
        }
        if (name == PropertyName._clickTween)
        {
            value = VariantUtils.CreateFrom(in _clickTween);
            return true;
        }
        if (name == PropertyName._hotkey)
        {
            value = VariantUtils.CreateFrom(in _hotkey);
            return true;
        }
        return base.GetGodotClassPropertyValue(in name, out value);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static new List<PropertyInfo> GetGodotPropertyList()
    {
        List<PropertyInfo> list = new List<PropertyInfo>();
        list.Add(
            new PropertyInfo(
                Variant.Type.String,
                PropertyName.ClickedSfx,
                PropertyHint.None,
                "",
                PropertyUsageFlags.ScriptVariable,
                exported: false
            )
        );
        list.Add(
            new PropertyInfo(
                Variant.Type.String,
                PropertyName.HoveredSfx,
                PropertyHint.None,
                "",
                PropertyUsageFlags.ScriptVariable,
                exported: false
            )
        );
        list.Add(
            new PropertyInfo(
                Variant.Type.Object,
                PropertyName._buttonAnimator,
                PropertyHint.None,
                "",
                PropertyUsageFlags.ScriptVariable,
                exported: false
            )
        );
        list.Add(
            new PropertyInfo(
                Variant.Type.Object,
                PropertyName._label,
                PropertyHint.None,
                "",
                PropertyUsageFlags.ScriptVariable,
                exported: false
            )
        );
        list.Add(
            new PropertyInfo(
                Variant.Type.Object,
                PropertyName._tween,
                PropertyHint.None,
                "",
                PropertyUsageFlags.ScriptVariable,
                exported: false
            )
        );
        list.Add(
            new PropertyInfo(
                Variant.Type.Object,
                PropertyName._clickTween,
                PropertyHint.None,
                "",
                PropertyUsageFlags.ScriptVariable,
                exported: false
            )
        );
        list.Add(
            new PropertyInfo(
                Variant.Type.StringName,
                PropertyName._hotkey,
                PropertyHint.None,
                "",
                PropertyUsageFlags.ScriptVariable,
                exported: false
            )
        );
        return list;
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    protected override void SaveGodotObjectData(GodotSerializationInfo info)
    {
        base.SaveGodotObjectData(info);
        info.AddProperty(PropertyName._buttonAnimator, Variant.From(in _buttonAnimator));
        info.AddProperty(PropertyName._label, Variant.From(in _label));
        info.AddProperty(PropertyName._tween, Variant.From(in _tween));
        info.AddProperty(PropertyName._clickTween, Variant.From(in _clickTween));
        info.AddProperty(PropertyName._hotkey, Variant.From(in _hotkey));
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    protected override void RestoreGodotObjectData(GodotSerializationInfo info)
    {
        base.RestoreGodotObjectData(info);
        if (info.TryGetProperty(PropertyName._buttonAnimator, out var value))
        {
            _buttonAnimator = value.As<Control>();
        }
        if (info.TryGetProperty(PropertyName._label, out var value2))
        {
            _label = value2.As<MegaLabel>();
        }
        if (info.TryGetProperty(PropertyName._tween, out var value3))
        {
            _tween = value3.As<Tween>();
        }
        if (info.TryGetProperty(PropertyName._clickTween, out var value4))
        {
            _clickTween = value4.As<Tween>();
        }
        if (info.TryGetProperty(PropertyName._hotkey, out var value5))
        {
            _hotkey = value5.As<StringName>();
        }
    }
}
