# Contributing

Thanks for helping improve Global Team Widget.

## Prerequisites

- Windows 11
- Visual Studio 2022 17.8 or later
- .NET SDK 10.0
- Windows App SDK / WinUI 3 workload
- .NET desktop development workload
- x64 architecture

## Setup

1. Clone the repository.
2. Open GlobalTeamWidget.sln in Visual Studio.
3. Set the startup project to GlobalTeamWidget.
4. Build and run the solution.

## Build and test

From the repository root:

```powershell
dotnet restore GlobalTeamWidget.sln
dotnet build GlobalTeamWidget.sln -c Debug
dotnet test GlobalTeamWidget.Tests/GlobalTeamWidget.Tests.csproj
```

## Contribution expectations

- Keep changes focused and well-scoped.
- Add or update tests when behavior changes.
- Update the changelog for user-visible changes.
- Avoid committing secrets or environment-specific values.
- Open an issue first for larger feature work when the scope is unclear.

## Pull requests

- Include a clear summary of the change.
- Mention tests run and any screenshots or recordings.
- Link related issues where applicable.
- Keep the PR description aligned with the repository template.
