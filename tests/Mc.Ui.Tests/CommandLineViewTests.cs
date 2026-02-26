using Mc.Ui.Widgets;
using Terminal.Gui;
using Xunit;

namespace Mc.Ui.Tests;

/// <summary>
/// Tests for the command-line input strip at the bottom of the file manager.
/// Verifies directory display (prompt visibility) and command-entry behaviour.
/// </summary>
[Collection("TUI Tests")]
public sealed class CommandLineViewTests
{
    public CommandLineViewTests(ApplicationFixture _) { }

    private static readonly string Home =
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    // ------------------------------------------------------------------
    // Helper: read the prompt Label text (first subview)
    // ------------------------------------------------------------------

    private static string GetPrompt(CommandLineView view)
    {
        // Subviews[0] = _prompt (Label), Subviews[1] = _input (TextField)
        var label = (Label)view.Subviews[0];
        return label.Text?.ToString() ?? string.Empty;
    }

    // ==================================================================
    // SetDirectory — prompt visibility
    // ==================================================================

    [Fact]
    public void SetDirectory_PathUnderHome_PromptStartsWithTilde()
    {
        var view = new CommandLineView();
        // One sub-directory → ["~", "Documents"] = 2 parts → no truncation → starts with "~/"
        view.SetDirectory(Home + "/Documents");
        Assert.StartsWith("~/", GetPrompt(view));
    }

    [Fact]
    public void SetDirectory_PathExactlyHome_PromptShowsTildeOnly()
    {
        var view = new CommandLineView();
        view.SetDirectory(Home);
        var prompt = GetPrompt(view);
        Assert.StartsWith("~", prompt);
        // The trailing "> " must always be present
        Assert.EndsWith("> ", prompt);
    }

    [Fact]
    public void SetDirectory_NonHomePath_PromptShowsPath()
    {
        var view = new CommandLineView();
        view.SetDirectory("/etc/ssl/certs");
        var prompt = GetPrompt(view);
        // Must not start with ~ (it's not under home)
        Assert.DoesNotContain("~", prompt.Split('>')[0]);
        Assert.EndsWith("> ", prompt);
    }

    [Fact]
    public void SetDirectory_ShortPath_PromptIsNotTruncated()
    {
        var view = new CommandLineView();
        view.SetDirectory("/tmp");
        var prompt = GetPrompt(view);
        // "/tmp" has only 1 component — no truncation needed
        Assert.DoesNotContain("…", prompt);
        Assert.EndsWith("> ", prompt);
    }

    [Fact]
    public void SetDirectory_DeepPath_PromptShowsEllipsis()
    {
        var view = new CommandLineView();
        view.SetDirectory("/very/deep/nested/path/inside/the/filesystem");
        var prompt = GetPrompt(view);
        // Path has > 2 components, so it should be shortened with "…"
        Assert.Contains("…", prompt);
        Assert.EndsWith("> ", prompt);
    }

    [Fact]
    public void SetDirectory_AlwaysEndsWithPromptSuffix()
    {
        var view = new CommandLineView();
        foreach (var path in new[] { "/", "/usr", "/usr/local/bin", Home })
        {
            view.SetDirectory(path);
            Assert.EndsWith("> ", GetPrompt(view));
        }
    }

    [Fact]
    public void SetDirectory_TwoComponentPath_ShowsBothComponents()
    {
        var view = new CommandLineView();
        // "/bin" splits as ["", "bin"] = 2 parts → no truncation
        view.SetDirectory("/bin");
        var prompt = GetPrompt(view);
        Assert.DoesNotContain("…", prompt);
        Assert.Contains("bin", prompt);
    }

    // ==================================================================
    // CommandEntered event — command dispatch
    // ==================================================================

    [Fact]
    public void CommandEntered_Event_ExposedOnClass()
    {
        // Verify the event exists and can be subscribed to
        var view = new CommandLineView();
        string? received = null;
        view.CommandEntered += (_, cmd) => received = cmd;
        // Event exists (no exception subscribing)
        Assert.Null(received);  // Nothing fired yet — just checking wiring
    }

    // ==================================================================
    // Focus helper
    // ==================================================================

    [Fact]
    public void Focus_DoesNotThrow()
    {
        var view = new CommandLineView();
        var ex = Record.Exception(() => view.Focus());
        Assert.Null(ex);
    }
}
