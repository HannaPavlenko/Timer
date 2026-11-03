using System;
using System.Linq;
using Avalonia.Controls;

namespace Timer.Controls;

public sealed class DigitsOnlyTextBox : TextBox
{
    protected override Type StyleKeyOverride => typeof(TextBox);

    protected override string? CoerceText(string? value)
    {
        if (value is null || value.All(character => character is >= '0' and <= '9'))
            return base.CoerceText(value);

        return base.CoerceText(new string(value.Where(character => character is >= '0' and <= '9').ToArray()));
    }
}
