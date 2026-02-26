# Contributing

Thank you for considering a contribution to Midnight Commander for .NET.

## Development Setup

1. Install [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
2. Clone the repository and open the solution:
   ```bash
   git clone https://github.com/crowndip/MCCSharpFork.git
   cd MCCSharpFork
   dotnet build
   dotnet test
   ```

## Code Style

- Target framework: **net8.0**
- Nullable reference types enabled — no `#nullable disable`
- Use `var` where the type is obvious from the right-hand side
- Prefer expression-bodied members for single-line getters/methods
- All public API members should have XML doc comments (`/// <summary>`)
- Dialogs live in `src/Mc.Ui/Dialogs/`, widgets in `src/Mc.Ui/Widgets/`
- Keep Terminal.Gui v2 API usage consistent with existing code (see `McTheme.cs` and `McApplication.cs` for patterns)

## Terminal.Gui v2 Patterns

This project targets Terminal.Gui **v2**. Key differences from v1:

| v1 | v2 |
|----|----|
| `Button.Clicked +=` | `Button.Accepting += (_, _) =>` |
| `CheckBox.Checked` | `CheckBox.CheckedState == CheckState.Checked` |
| `ListView.SetSource(list)` | `ListView.SetSource(new ObservableCollection<T>(list))` |
| `Application.MainLoop?.Invoke` | `Application.Invoke` |
| `Application.Refresh()` | `Application.LayoutAndDraw(true)` |
| `Bounds.Width` | `Viewport.Width` |
| `SetNeedsDisplay()` | `SetNeedsDraw()` |

## Pull Request Guidelines

1. Branch from `main`: `git checkout -b feature/my-feature`
2. Run `dotnet build` — must produce **0 errors**
3. Run `dotnet test` — all tests must **pass**
4. Keep PRs focused: one feature or fix per PR
5. Update `README.md` if you add user-visible behaviour
6. All commits must be signed-off (`git commit -s`)

## Reporting Bugs

Open an issue and include:
- OS and terminal emulator
- .NET version (`dotnet --version`)
- Steps to reproduce
- Expected vs actual behaviour

## License

By contributing you agree that your contributions will be licensed under the GNU GPL v3.
