using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Timer.Controls;

public sealed class DigitsOnlyTextBox : TextBox
{
    private bool _clearedZero;

    public DigitsOnlyTextBox()
    {
        AddHandler(TextInputEvent, RejectNonDigitTextInput, RoutingStrategies.Tunnel);
        if (OperatingSystem.IsWindows())
            InputMethod.SetIsInputMethodEnabled(this, false);
    }

    protected override Type StyleKeyOverride => typeof(TextBox);

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        ClearZeroForEditing();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (IsFocused)
            ClearZeroForEditing();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        if (_clearedZero && string.IsNullOrEmpty(Text))
            SetCurrentValue(TextProperty, "0");

        _clearedZero = false;
    }

    private void ClearZeroForEditing()
    {
        if (!IsReadOnly && Text == "0")
        {
            _clearedZero = true;
            SetCurrentValue(TextProperty, string.Empty);
        }
    }

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
