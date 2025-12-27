# Isolated NUnit Tests

This folder contains an isolated NUnit test project that references the main EndlessNight project but is not included in the main solution file. You can run it directly via dotnet CLI.

## Run
```powershell
cd C:\Projects\Github\Console\Endless-Night\Tests\EndlessNight.Tests
dotnet restore
dotnet test
```

## Notes
- TargetFramework: net10.0
- Packages: Microsoft.NET.Test.Sdk, NUnit, NUnit3TestAdapter
- If your IDE doesn't auto-detect this project, open it directly or run via CLI.

