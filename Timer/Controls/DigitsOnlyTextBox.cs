using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Timer.Controls;

public sealed class DigitsOnlyTextBox : TextBox
{
    public DigitsOnlyTextBox()
    {
        AddHandler(TextInputEvent, RejectNonDigitTextInput, RoutingStrategies.Tunnel);
        if (OperatingSystem.IsWindows())
            InputMethod.SetIsInputMethodEnabled(this, false);
    }

    protected override Type StyleKeyOverride => typeof(TextBox);

    private static void RejectNonDigitTextInput(object? sender, TextInputEventArgs e)
    {
        if (e.Text?.Any(character => character is < '0' or > '9') == true)
            e.Handled = true;
    }

    protected override string? CoerceText(string? value)
    {
        if (value is null || value.All(character => character is >= '0' and <= '9'))
            return base.CoerceText(value);

        return base.CoerceText(new string(value.Where(character => character is >= '0' and <= '9').ToArray()));
    }
}
