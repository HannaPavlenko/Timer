using Microsoft.VisualStudio.TestTools.UnitTesting;
using Avalonia.Controls;
using Timer.Controls;
using Timer.ViewModels;

namespace Timer.App.Tests;

[TestClass]
public sealed class MainViewModelTests
{
    [TestMethod]
    public void NumericTimeInputKeepsOnlyAsciiDigits()
    {
        var input = new DigitsOnlyTextBox { Text = "вап12a3" };

        Assert.AreEqual(typeof(TextBox), input.StyleKey);
        Assert.AreEqual("123", input.Text);
        input.Text = "4 5-6";
        Assert.AreEqual("456", input.Text);
    }

    [TestMethod]
    public void CountdownShowsPresetAndRejectsInvalidSeconds()
    {
        var viewModel = new MainViewModel();
        viewModel.SelectCountdownCommand.Execute(null);
        viewModel.DurationMinutes = "5";

        Assert.AreEqual("05:00", viewModel.DisplayTime);
        Assert.IsTrue(viewModel.IsDurationMode);

        viewModel.DurationSeconds = "75";
        Assert.IsTrue(viewModel.HasError);
        Assert.AreEqual("00:00", viewModel.DisplayTime);

        viewModel.DurationSeconds = "30";
        Assert.IsFalse(viewModel.HasError);
        Assert.AreEqual("05:30", viewModel.DisplayTime);
    }

    [TestMethod]
    public void StartPauseResumeAndResetUpdateControls()
    {
        var viewModel = new MainViewModel();
        viewModel.SelectCountUpCommand.Execute(null);
        viewModel.DurationMinutes = "0";
        viewModel.DurationSeconds = "5";

        viewModel.StartOrPauseCommand.Execute(null);
        Assert.AreEqual("RUNNING", viewModel.StatusLabel);
        Assert.AreEqual("Pause timer", viewModel.PrimaryActionLabel);
        Assert.IsFalse(viewModel.CanEditSettings);

        viewModel.StartOrPauseCommand.Execute(null);
        Assert.AreEqual("PAUSED", viewModel.StatusLabel);
        Assert.AreEqual("Resume timer", viewModel.PrimaryActionLabel);

        viewModel.StartOrPauseCommand.Execute(null);
        Assert.AreEqual("RUNNING", viewModel.StatusLabel);

        viewModel.ResetCommand.Execute(null);
        Assert.AreEqual("READY", viewModel.StatusLabel);
        Assert.AreEqual("00:00", viewModel.DisplayTime);
        Assert.IsTrue(viewModel.CanEditSettings);
    }

    [TestMethod]
    public void SwitchingModesResetsTheSession()
    {
        var viewModel = new MainViewModel();
        Assert.IsFalse(viewModel.HasDetailLabel);
        viewModel.StartOrPauseCommand.Execute(null);
        Assert.AreEqual("RUNNING", viewModel.StatusLabel);

        viewModel.WorkSeconds = "45";
        viewModel.RestSeconds = "15";
        viewModel.Rounds = "4";
        viewModel.SelectedModeIndex = 3;
        Assert.IsTrue(viewModel.IsIntervalsSelected);
        Assert.AreEqual(3, viewModel.SelectedModeIndex);
        Assert.AreEqual("READY", viewModel.StatusLabel);
        Assert.AreEqual("0", viewModel.PreparationSeconds);
        Assert.AreEqual("00:45", viewModel.DisplayTime);
        Assert.AreEqual("WORK INTERVAL", viewModel.PhaseLabel);
        Assert.AreEqual("Round 1 of 4  ·  work", viewModel.DetailLabel);
        Assert.IsTrue(viewModel.CanEditSettings);

        viewModel.SelectCountdownCommand.Execute(null);
        Assert.AreEqual(2, viewModel.SelectedModeIndex);
        Assert.IsTrue(viewModel.IsDurationMode);
    }

    [TestMethod]
    public void CountUpAndCountdownKeepSeparateTargets()
    {
        var viewModel = new MainViewModel();
        viewModel.SelectCountUpCommand.Execute(null);
        viewModel.DurationMinutes = "1";
        viewModel.DurationSeconds = "20";

        viewModel.SelectCountdownCommand.Execute(null);
        viewModel.DurationMinutes = "2";
        viewModel.DurationSeconds = "30";
        Assert.AreEqual("02:30", viewModel.DisplayTime);

        viewModel.SelectCountUpCommand.Execute(null);
        Assert.AreEqual("1", viewModel.DurationMinutes);
        Assert.AreEqual("20", viewModel.DurationSeconds);
        Assert.AreEqual("Goal  01:20", viewModel.DetailLabel);

        viewModel.SelectCountdownCommand.Execute(null);
        Assert.AreEqual("2", viewModel.DurationMinutes);
        Assert.AreEqual("30", viewModel.DurationSeconds);
        Assert.AreEqual("02:30", viewModel.DisplayTime);
    }

    [TestMethod]
    public void SixSecondsStartsWithEmptyMinutesInBothDurationModes()
    {
        var viewModel = new MainViewModel();
        viewModel.SelectCountUpCommand.Execute(null);
        viewModel.DurationMinutes = "";
        viewModel.DurationSeconds = "6";

        Assert.IsFalse(viewModel.HasError);
        Assert.AreEqual("Goal  00:06", viewModel.DetailLabel);
        viewModel.StartOrPauseCommand.Execute(null);
        Assert.AreEqual("RUNNING", viewModel.StatusLabel);

        viewModel.SelectCountdownCommand.Execute(null);
        viewModel.DurationMinutes = "";
        viewModel.DurationSeconds = "6";

        Assert.IsFalse(viewModel.HasError);
        Assert.AreEqual("00:06", viewModel.DisplayTime);
        viewModel.StartOrPauseCommand.Execute(null);
        Assert.AreEqual("RUNNING", viewModel.StatusLabel);
    }
}
