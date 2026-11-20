using System.Collections.Immutable;
using OpenClaw.Chat;
using Xunit;

namespace OpenClaw.Shared.Tests;

public sealed class ChatTimelineEditingTests
{
    [Fact]
    public void EditUserMessage_UpdatesTextAndPreservesOriginal()
    {
        var state = ChatTimelineState.Initial();
        var userEntry = new ChatTimelineItem("msg-1", ChatTimelineItemKind.User, "Initial prompt");
        var assistantEntry = new ChatTimelineItem("msg-2", ChatTimelineItemKind.Assistant, "Assistant reply");

        state = state with { Entries = ImmutableList.Create(userEntry, assistantEntry) };

        var updated = ChatTimelineReducer.EditUserMessage(state, "msg-1", "Edited prompt", truncateDownstream: false);

        Assert.Equal(2, updated.Entries.Count);
        var editedUser = updated.Entries[0];
        Assert.Equal("Edited prompt", editedUser.Text);
        Assert.True(editedUser.IsEdited);
        Assert.Equal("Initial prompt", editedUser.OriginalText);
    }

    [Fact]
    public void EditUserMessage_WithTruncateDownstream_RemovesInvalidDownstreamResponses()
    {
        var state = ChatTimelineState.Initial();
        var user1 = new ChatTimelineItem("msg-1", ChatTimelineItemKind.User, "First prompt");
        var asst1 = new ChatTimelineItem("msg-2", ChatTimelineItemKind.Assistant, "First reply");
        var user2 = new ChatTimelineItem("msg-3", ChatTimelineItemKind.User, "Second prompt");
        var asst2 = new ChatTimelineItem("msg-4", ChatTimelineItemKind.Assistant, "Second reply");

        state = state with { Entries = ImmutableList.Create(user1, asst1, user2, asst2) };

        var updated = ChatTimelineReducer.EditUserMessage(state, "msg-1", "New first prompt", truncateDownstream: true);

        Assert.Single(updated.Entries);
        Assert.Equal("msg-1", updated.Entries[0].Id);
        Assert.Equal("New first prompt", updated.Entries[0].Text);
        Assert.True(updated.Entries[0].IsEdited);
    }

    [Fact]
    public void PrepareRegeneration_PreservesExistingAsAlternative()
    {
        var state = ChatTimelineState.Initial();
        var user = new ChatTimelineItem("msg-1", ChatTimelineItemKind.User, "Explain recursion");
        var asst = new ChatTimelineItem("msg-2", ChatTimelineItemKind.Assistant, "Recursion is when a function calls itself.");

        state = state with { Entries = ImmutableList.Create(user, asst) };

        var regenerated = ChatTimelineReducer.PrepareRegeneration(state, "msg-2", "qwen2.5-coder:7b");

        var regAsst = regenerated.Entries[1];
        Assert.True(regAsst.IsStreaming);
        Assert.Equal(string.Empty, regAsst.Text);
        Assert.NotNull(regAsst.AlternativeResponses);
        Assert.Single(regAsst.AlternativeResponses);
        Assert.Equal("Recursion is when a function calls itself.", regAsst.AlternativeResponses[0]);
        Assert.Equal("qwen2.5-coder:7b", regAsst.ModelUsed);
        Assert.Equal(2, regAsst.AlternativeCount);
    }

    [Fact]
    public void SelectAlternative_SwitchesVisibleResponse()
    {
        var asst = new ChatTimelineItem(
            "msg-2",
            ChatTimelineItemKind.Assistant,
            "Latest response text",
            AlternativeResponses: new[] { "Original response text" },
            ActiveAlternativeIndex: 0);

        Assert.Equal("Latest response text", asst.ActiveText);

        var state = ChatTimelineState.Initial() with { Entries = ImmutableList.Create(asst) };
        var switched = ChatTimelineReducer.SelectAlternative(state, "msg-2", 1);

        var switchedItem = switched.Entries[0];
        Assert.Equal(1, switchedItem.ActiveAlternativeIndex);
        Assert.Equal("Original response text", switchedItem.ActiveText);
    }

    [Fact]
    public void TogglePinMessage_TogglesPinnedState()
    {
        var item = new ChatTimelineItem("msg-1", ChatTimelineItemKind.User, "Bookmark this");
        var state = ChatTimelineState.Initial() with { Entries = ImmutableList.Create(item) };

        Assert.False(state.Entries[0].IsPinned);

        var pinned = ChatTimelineReducer.TogglePinMessage(state, "msg-1");
        Assert.True(pinned.Entries[0].IsPinned);

        var unpinned = ChatTimelineReducer.TogglePinMessage(pinned, "msg-1");
        Assert.False(unpinned.Entries[0].IsPinned);
    }

    [Fact]
    public void SetComparisonResponses_AttachesSideBySideModels()
    {
        var item = new ChatTimelineItem("msg-1", ChatTimelineItemKind.Assistant, "Primary");
        var state = ChatTimelineState.Initial() with { Entries = ImmutableList.Create(item) };

        var comparisons = new[]
        {
            new ChatComparisonResponse("qwen2.5-coder:7b", "Code response A", ElapsedMs: 120),
            new ChatComparisonResponse("gpt-4o", "Code response B", ElapsedMs: 180)
        };

        var compared = ChatTimelineReducer.SetComparisonResponses(state, "msg-1", comparisons);
        Assert.NotNull(compared.Entries[0].ComparisonResponses);
        Assert.Equal(2, compared.Entries[0].ComparisonResponses!.Count);
        Assert.Equal("qwen2.5-coder:7b", compared.Entries[0].ComparisonResponses![0].ModelName);
        Assert.Equal("gpt-4o", compared.Entries[0].ComparisonResponses![1].ModelName);
    }
}
