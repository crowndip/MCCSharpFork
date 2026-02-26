using Terminal.Gui;
using Xunit;

namespace Mc.Ui.Tests;

/// <summary>
/// Initialises Terminal.Gui with a headless FakeDriver once for the
/// entire "TUI Tests" collection.  Terminal.Gui 2.x cannot be re-initialised
/// per-test (global state persists across Shutdown/Init cycles), so all
/// UI tests share this single fixture.
/// </summary>
public sealed class ApplicationFixture : IDisposable
{
    public ApplicationFixture() => Application.Init(new FakeDriver());
    public void Dispose() => Application.Shutdown();
}

/// <summary>
/// xUnit collection that shares a single <see cref="ApplicationFixture"/>
/// across every UI test class in this project.
/// </summary>
[CollectionDefinition("TUI Tests")]
public class TuiCollection : ICollectionFixture<ApplicationFixture> { }
