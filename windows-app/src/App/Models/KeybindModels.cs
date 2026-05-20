namespace FieldForce.App.Models;

public enum KeybindAction
{
    ToggleFfb,
    EmergencyStop,
    Reload,
    ToggleOverlay,
    ToggleOverlayClickThrough
}

public enum InputBindingKind
{
    None,
    Keyboard,
    DirectInputButton
}

[Flags]
public enum KeyboardModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Windows = 8
}

public sealed class InputBinding : IEquatable<InputBinding>
{
    public InputBindingKind Kind { get; set; } = InputBindingKind.None;
    public int? VirtualKey { get; set; }
    public KeyboardModifiers Modifiers { get; set; }
    public string? DeviceStableId { get; set; }
    public string? DeviceName { get; set; }
    public int? ButtonIndex { get; set; }

    public static InputBinding None() => new();

    public static InputBinding Keyboard(int virtualKey, KeyboardModifiers modifiers) => new()
    {
        Kind = InputBindingKind.Keyboard,
        VirtualKey = virtualKey,
        Modifiers = modifiers
    };

    public static InputBinding DirectInputButton(string deviceStableId, string deviceName, int buttonIndex) => new()
    {
        Kind = InputBindingKind.DirectInputButton,
        DeviceStableId = deviceStableId,
        DeviceName = deviceName,
        ButtonIndex = buttonIndex
    };

    public bool IsNone => Kind == InputBindingKind.None;

    public string DisplayText => Kind switch
    {
        InputBindingKind.Keyboard => FormatKeyboard(),
        InputBindingKind.DirectInputButton => $"{(string.IsNullOrWhiteSpace(DeviceName) ? "DirectInput device" : DeviceName)} Button {ButtonIndex.GetValueOrDefault() + 1}",
        _ => "None"
    };

    public bool Equals(InputBinding? other)
    {
        if (other is null || Kind != other.Kind)
        {
            return false;
        }

        return Kind switch
        {
            InputBindingKind.None => true,
            InputBindingKind.Keyboard => VirtualKey == other.VirtualKey && Modifiers == other.Modifiers,
            InputBindingKind.DirectInputButton => string.Equals(DeviceStableId, other.DeviceStableId, StringComparison.OrdinalIgnoreCase) &&
                                                 ButtonIndex == other.ButtonIndex,
            _ => false
        };
    }

    public override bool Equals(object? obj) => obj is InputBinding binding && Equals(binding);

    public override int GetHashCode() => Kind switch
    {
        InputBindingKind.Keyboard => HashCode.Combine(Kind, VirtualKey, Modifiers),
        InputBindingKind.DirectInputButton => HashCode.Combine(Kind, DeviceStableId?.ToUpperInvariant(), ButtonIndex),
        _ => Kind.GetHashCode()
    };

    private string FormatKeyboard()
    {
        var parts = new List<string>();
        if (Modifiers.HasFlag(KeyboardModifiers.Control))
        {
            parts.Add("Ctrl");
        }

        if (Modifiers.HasFlag(KeyboardModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (Modifiers.HasFlag(KeyboardModifiers.Shift))
        {
            parts.Add("Shift");
        }

        if (Modifiers.HasFlag(KeyboardModifiers.Windows))
        {
            parts.Add("Win");
        }

        parts.Add(KeyboardVirtualKeyNames.Format(VirtualKey.GetValueOrDefault()));
        return string.Join("+", parts);
    }
}

public sealed class KeybindsConfig
{
    public InputBinding ToggleFfb { get; set; } = InputBinding.None();
    public InputBinding EmergencyStop { get; set; } = InputBinding.None();
    public InputBinding Reload { get; set; } = InputBinding.None();
    public InputBinding ToggleOverlay { get; set; } = InputBinding.None();
    public InputBinding ToggleOverlayClickThrough { get; set; } = InputBinding.None();

    public InputBinding Get(KeybindAction action) => action switch
    {
        KeybindAction.ToggleFfb => ToggleFfb,
        KeybindAction.EmergencyStop => EmergencyStop,
        KeybindAction.Reload => Reload,
        KeybindAction.ToggleOverlay => ToggleOverlay,
        KeybindAction.ToggleOverlayClickThrough => ToggleOverlayClickThrough,
        _ => InputBinding.None()
    };

    public void Set(KeybindAction action, InputBinding binding)
    {
        switch (action)
        {
            case KeybindAction.ToggleFfb:
                ToggleFfb = binding;
                break;
            case KeybindAction.EmergencyStop:
                EmergencyStop = binding;
                break;
            case KeybindAction.Reload:
                Reload = binding;
                break;
            case KeybindAction.ToggleOverlay:
                ToggleOverlay = binding;
                break;
            case KeybindAction.ToggleOverlayClickThrough:
                ToggleOverlayClickThrough = binding;
                break;
        }
    }

    public static IReadOnlyList<KeybindAction> Actions { get; } =
    [
        KeybindAction.ToggleFfb,
        KeybindAction.EmergencyStop,
        KeybindAction.Reload,
        KeybindAction.ToggleOverlay,
        KeybindAction.ToggleOverlayClickThrough
    ];
}

public static class KeyboardVirtualKeyNames
{
    public static string Format(int virtualKey)
    {
        if (virtualKey is >= 0x30 and <= 0x39 || virtualKey is >= 0x41 and <= 0x5A)
        {
            return ((char)virtualKey).ToString();
        }

        return virtualKey switch
        {
            0x08 => "Backspace",
            0x09 => "Tab",
            0x0D => "Enter",
            0x13 => "Pause",
            0x1B => "Esc",
            0x20 => "Space",
            >= 0x70 and <= 0x87 => $"F{virtualKey - 0x6F}",
            _ => $"VK 0x{virtualKey:X2}"
        };
    }
}
