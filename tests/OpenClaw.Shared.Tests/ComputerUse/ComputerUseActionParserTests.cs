using System.Linq;
using OpenClaw.Shared.ComputerUse.Actions;
using OpenClaw.Shared.ComputerUse.Types;
using Xunit;

namespace OpenClaw.Shared.Tests.ComputerUse;

public class ComputerUseActionParserTests
{
    private readonly ComputerUseActionParser _parser = new();

    [Fact]
    public void ParsePrediction_ClickAction_ReturnsAccurateCoordinates()
    {
        var raw = "Thought: Need to click the submit button.\nAction: click(start_box='[100,200,100,200]')";
        var prediction = _parser.ParsePrediction(raw, screenWidth: 1920, screenHeight: 1080);

        Assert.Equal("Need to click the submit button.", prediction.Thought);
        Assert.Single(prediction.Actions);

        var action = prediction.Actions[0];
        Assert.Equal(ComputerUseActionType.Click, action.ActionType);
        Assert.NotNull(action.StartBox);

        // Normalized: (100+100)/2 = 100, (200+200)/2 = 200
        // Scaled to 1920x1080: 100 * 1920 / 1000 = 192, 200 * 1080 / 1000 = 216
        var point = action.StartBox.ToLogicalPoint(1920, 1080);
        Assert.Equal(192, point.X);
        Assert.Equal(216, point.Y);
    }

    [Fact]
    public void ParsePrediction_LeftDoubleClick_ReturnsLeftDoubleClickType()
    {
        var raw = "Action: left_double(start_box='[500, 500]')";
        var prediction = _parser.ParsePrediction(raw, 1000, 1000);

        Assert.Single(prediction.Actions);
        var action = prediction.Actions[0];
        Assert.Equal(ComputerUseActionType.DoubleClick, action.ActionType);
        Assert.NotNull(action.StartBox);
        var point = action.StartBox.ToLogicalPoint(1000, 1000);
        Assert.Equal(500, point.X);
        Assert.Equal(500, point.Y);
    }

    [Fact]
    public void ParsePrediction_RightClick_ReturnsRightClickType()
    {
        var raw = "Action: right_single(start_box='[300, 400]')";
        var prediction = _parser.ParsePrediction(raw, 1000, 1000);

        Assert.Single(prediction.Actions);
        Assert.Equal(ComputerUseActionType.RightClick, prediction.Actions[0].ActionType);
        var point = prediction.Actions[0].StartBox?.ToLogicalPoint(1000, 1000);
        Assert.Equal(300, point?.X);
        Assert.Equal(400, point?.Y);
    }

    [Fact]
    public void ParsePrediction_MiddleClick_ReturnsMiddleClickType()
    {
        var raw = "Action: middle_click(start_box='[150, 250]')";
        var prediction = _parser.ParsePrediction(raw, 1000, 1000);

        Assert.Single(prediction.Actions);
        Assert.Equal(ComputerUseActionType.MiddleClick, prediction.Actions[0].ActionType);
        var point = prediction.Actions[0].StartBox?.ToLogicalPoint(1000, 1000);
        Assert.Equal(150, point?.X);
        Assert.Equal(250, point?.Y);
    }

    [Fact]
    public void ParsePrediction_MouseMove_ReturnsMouseMoveType()
    {
        var raw = "Action: mouse_move(start_box='[450, 550]')";
        var prediction = _parser.ParsePrediction(raw, 1000, 1000);

        Assert.Single(prediction.Actions);
        Assert.Equal(ComputerUseActionType.MouseMove, prediction.Actions[0].ActionType);
        var point = prediction.Actions[0].StartBox?.ToLogicalPoint(1000, 1000);
        Assert.Equal(450, point?.X);
        Assert.Equal(550, point?.Y);
    }

    [Fact]
    public void ParsePrediction_DragAction_ReturnsStartAndEndCoordinates()
    {
        var raw = "Action: drag(start_box='[100, 100]', end_box='[300, 400]')";
        var prediction = _parser.ParsePrediction(raw, 1000, 1000);

        Assert.Single(prediction.Actions);
        var action = prediction.Actions[0];
        Assert.Equal(ComputerUseActionType.Drag, action.ActionType);
        Assert.NotNull(action.StartBox);
        Assert.NotNull(action.EndBox);
        var startPoint = action.StartBox.ToLogicalPoint(1000, 1000);
        var endPoint = action.EndBox.ToLogicalPoint(1000, 1000);
        Assert.Equal(100, startPoint.X);
        Assert.Equal(100, startPoint.Y);
        Assert.Equal(300, endPoint.X);
        Assert.Equal(400, endPoint.Y);
    }

    [Fact]
    public void ParsePrediction_TypeText_ReturnsContent()
    {
        var raw = "Thought: Type search query\nAction: type(content='SPARK Native Computer Use')";
        var prediction = _parser.ParsePrediction(raw, 1000, 1000);

        Assert.Single(prediction.Actions);
        var action = prediction.Actions[0];
        Assert.Equal(ComputerUseActionType.TypeText, action.ActionType);
        Assert.Equal("SPARK Native Computer Use", action.TextContent);
    }

    [Fact]
    public void ParsePrediction_Hotkey_ReturnsKeyCombination()
    {
        var raw = "Action: hotkey(key='Ctrl+Shift+Esc')";
        var prediction = _parser.ParsePrediction(raw, 1000, 1000);

        Assert.Single(prediction.Actions);
        var action = prediction.Actions[0];
        Assert.Equal(ComputerUseActionType.Hotkey, action.ActionType);
        Assert.Equal("ctrl+shift+esc", action.Key?.ToLowerInvariant());
    }

    [Fact]
    public void ParsePrediction_PressAndRelease_ReturnsCorrespondingTypes()
    {
        var rawPress = "Action: press(key='Shift')";
        var predictionPress = _parser.ParsePrediction(rawPress, 1000, 1000);
        Assert.Equal(ComputerUseActionType.KeyPress, predictionPress.Actions[0].ActionType);
        Assert.Equal("Shift", predictionPress.Actions[0].Key);

        var rawRelease = "Action: release(key='Shift')";
        var predictionRelease = _parser.ParsePrediction(rawRelease, 1000, 1000);
        Assert.Equal(ComputerUseActionType.KeyRelease, predictionRelease.Actions[0].ActionType);
        Assert.Equal("Shift", predictionRelease.Actions[0].Key);
    }

    [Fact]
    public void ParsePrediction_Scroll_ReturnsDirectionAndCoordinates()
    {
        var raw = "Action: scroll(start_box='[500, 500]', direction='down')";
        var prediction = _parser.ParsePrediction(raw, 1000, 1000);

        Assert.Single(prediction.Actions);
        var action = prediction.Actions[0];
        Assert.Equal(ComputerUseActionType.Scroll, action.ActionType);
        Assert.Equal(ScrollDirection.Down, action.Direction);
    }

    [Fact]
    public void ParsePrediction_Wait_ReturnsDuration()
    {
        var raw = "Action: wait()";
        var prediction = _parser.ParsePrediction(raw, 1000, 1000);

        Assert.Single(prediction.Actions);
        var action = prediction.Actions[0];
        Assert.Equal(ComputerUseActionType.Wait, action.ActionType);
    }

    [Fact]
    public void ParsePrediction_Finished_ReturnsFinishedType()
    {
        var raw = "Thought: Task is complete.\nAction: finished()";
        var prediction = _parser.ParsePrediction(raw, 1000, 1000);

        Assert.Single(prediction.Actions);
        Assert.Equal(ComputerUseActionType.Finished, prediction.Actions[0].ActionType);
    }

    [Fact]
    public void ParsePrediction_CallUser_ReturnsCallUserType()
    {
        var raw = "Thought: Need human assistance for 2FA.\nAction: call_user()";
        var prediction = _parser.ParsePrediction(raw, 1000, 1000);

        Assert.Single(prediction.Actions);
        Assert.Equal(ComputerUseActionType.CallUser, prediction.Actions[0].ActionType);
    }

    [Fact]
    public void ParsePrediction_MalformedOrNoAction_ReturnsFinishedOrUnknown()
    {
        var raw = "I am not sure what to do next without a screen.";
        var prediction = _parser.ParsePrediction(raw, 1000, 1000);

        Assert.NotNull(prediction);
    }
}
