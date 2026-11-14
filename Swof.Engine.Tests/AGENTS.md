# Global working agreements for Codex

- Don't make things up. Read the source code, documenation, and answer based on that. If you are not sure, say it clear.

## Coding Standards

### C#

Coding standards:
- Always prefer using modern .Net features.
- Always use nullable reference types.
- Always perform null checks using `is null` or `is not null` instead of equality operators.
- Use pattern matching when checking multiple constant values, for example `enumVar is MyEnum.Val1 or MyEnum.Val2`.
- When validating parameters, use exception helper methods such as `ArgumentNullException.ThrowIfNull(variable)` whenever available.
- Always include curly braces when they are optional.
- Always give enum members an explicit value.
- Enum member values must always start with 1 UNLESS the enum includes a member such as 'None' or 'Not Provided'.
- Use sealed classes or records unless the type is meant to be extended.
- Make only high confidence suggestions when reviewing code changes.
- Write code with good maintainability practices, including comments on why certain design decisions were made.
- Handle edge cases and write clear exception handling.
- For libraries or external dependencies, mention their usage and purpose in comments.

Naming Conventions:
- Follow PascalCase for component names, method names, and public members.
- Use camelCase for private fields and local variables.
- Prefix interface names with "I" (e.g., IUserService).

Formatting:

- Formatting is verified with `dotnet format --verify-no-changes` using the default rules; there is no `.editorconfig` yet.
- Prefer file-scoped namespace declarations and single-line using directives.
- Insert a newline before the opening curly brace of any code block (e.g., after `if`, `for`, `while`, `foreach`, `using`, `try`, etc.).
- Ensure that the final return statement of a method is on its own line.
- Use pattern matching and switch expressions wherever possible.
- Use `nameof` instead of string literals when referring to member names.
- Ensure that XML doc comments are created for any public APIs. When applicable, include `<example>` and `<code>` documentation in the comments.

Tools:
- These standards apply to the whole repository, not only this folder.
- Run `tools/check.sh` (from the repository root) after making changes; it builds and runs the behavior scenarios. Pass `full` before handing off.
- The projects use nullable reference types. Warnings are not yet errors and no extra analyzer packages are installed, so read the build output for warnings.
- Do not rely on `dotnet test`: this project is an executable scenario runner and `dotnet test` does not execute it.

# SWOF Project Notes (Agent Prefill)

- `Swof.Engine` is a deterministic 60 Hz clean-room football simulation targeting SWOS feel, with strict tick authority (`StepTick`) and render interpolation between immutable snapshots. Its non-trivial behavior is centered on loose-ball dribbling (not glue possession), ball-control-dependent touch cadence/envelope, passive contest scoring, and one-button context actions (tap pass vs hold kick with lift/power curves).
- `Swof.Game` is the primary host (MonoGame DesktopGL): fixed-step simulation, interpolated presentation, batched rendering, keyboard and gamepad input.
- `Swof.Windows` is the legacy WPF host, retained for its live tuning grid. It is a thin host around the engine that intentionally keeps game logic out of UI code: it feeds keyboard input each frame, advances fixed-step simulation with a delta accumulator, and renders interpolated snapshots. The custom `FieldView` draws pitch lines, facing-aware player body geometry, active/possession rings, and an elevated ball with dynamic shadow scaling from ball height.
- `Swof.Engine.Tests` is a dependency-free executable test harness (no xUnit/NUnit) that validates behavior-level invariants directly against engine state. Coverage includes subtle mechanics such as interception during pass target switching, slide travel/recovery phases, low-control turn overrun, stationary lead variance by ball-control skill, and accumulator/tick equivalence.
- `docs/research` + `tools/swos` capture the clean-room reverse-engineering workflow: binary/data inventory from local SWOS assets, measurement scripts for DOSBox AVI timing/movement sampling, and explicit physics contracts that separate confirmed evidence from placeholder tunings. This research stream directly informs constants in `PhysicsTuning` rather than ad-hoc gameplay tweaks.
- `docs\research` also contains implementation-time notes captured while building specific game aspects (physics, ball control/dribbling, facing, movement measurement, and file inventory).
