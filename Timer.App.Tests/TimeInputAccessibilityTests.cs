using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Timer.Controls;
using Timer.Views;

namespace Timer.App.Tests;

/// <summary>Checks the label relationships exposed to accessibility services.</summary>
[TestClass]
public sealed class TimeInputAccessibilityTests
{
    /// <summary>Each numeric input exposes its own visible caption even when values match.</summary>
    [TestMethod]
    public async Task NumericInputsExposeTheirVisibleLabels()
    {
        await using HeadlessUnitTestSession session = HeadlessUnitTestSession.StartNew(typeof(Application));
        await session.Dispatch(CheckLabels, CancellationToken.None);
    }

    private static void CheckLabels()
    {
        MainView view = new();
        Window window = new() {Content = view};
        window.Show();

        try
        {
            Dictionary<string, DigitsOnlyTextBox> inputs = view.GetLogicalDescendants().OfType<DigitsOnlyTextBox>()
                .ToDictionary(input => AutomationProperties.GetName(input)!);

            Assert.AreEqual(9, inputs.Count);
            CheckLabel("Target time, minutes", "Minutes");
            CheckLabel("Target time, seconds", "Seconds");
            CheckLabel("Prepare time, minutes", "Prepare · min");
            CheckLabel("Prepare time, seconds", "Prepare · sec");
            CheckLabel("Work time, minutes", "Work · min");
            CheckLabel("Work time, seconds", "Work · sec");
            CheckLabel("Rest time, minutes", "Rest · min");
            CheckLabel("Rest time, seconds", "Rest · sec");
            CheckLabel("Interval rounds", "Rounds");

            void CheckLabel(string name, string caption)
            {
                DigitsOnlyTextBox input = inputs[name];
                input.SetCurrentValue(TextBox.TextProperty, "5");
                AutomationPeer peer = ControlAutomationPeer.CreatePeerForElement(input);
                Assert.AreEqual(caption, peer.GetLabeledBy()?.GetName(), name);
            }
        }
        finally
        {
            window.Close();
        }
    }
}
