using Microsoft.VisualStudio.TestTools.UnitTesting;
using Timer.ViewModels;

namespace Timer.App.Tests;

[TestClass]
public sealed class MainViewModelTests
{
    [TestMethod]
    public void CountdownShowsPresetAndRejectsInvalidSeconds()
    {
        var viewModel = new MainViewModel();
        viewModel.SelectCountdownCommand.Execute(null);

        Assert.AreEqual("05:00.0", viewModel.DisplayTime);
        Assert.IsTrue(viewModel.IsDurationMode);

        viewModel.DurationSeconds = "75";
        Assert.IsTrue(viewModel.HasError);
        Assert.AreEqual("--:--.-", viewModel.DisplayTime);

        viewModel.DurationSeconds = "30";
        Assert.IsFalse(viewModel.HasError);
        Assert.AreEqual("05:30.0", viewModel.DisplayTime);
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
        Assert.AreEqual("00:00.0", viewModel.DisplayTime);
        Assert.IsTrue(viewModel.CanEditSettings);
    }

    [TestMethod]
    public void SwitchingModesResetsTheSession()
    {
        var viewModel = new MainViewModel();
        viewModel.StartOrPauseCommand.Execute(null);
        Assert.AreEqual("RUNNING", viewModel.StatusLabel);

        viewModel.SelectIntervalsCommand.Execute(null);
        Assert.IsTrue(viewModel.IsIntervalsSelected);
        Assert.AreEqual("READY", viewModel.StatusLabel);
        Assert.AreEqual("00:45.0", viewModel.DisplayTime);
        Assert.AreEqual("Round 1 of 4  ·  work", viewModel.DetailLabel);
        Assert.IsTrue(viewModel.CanEditSettings);
    }
}
