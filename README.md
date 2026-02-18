# SMEH

SMEH is a small console app that helps you set up a Satisfactory modding environment on Windows. It installs : Visual Studio 2022, the Clang toolchain, the CSS Unreal Engine build, the SML starter project, and Wwise.

You need .NET 8 to run it. Build with `dotnet build` and run the exe, or use `dotnet run`. Default URLs and paths are in `AppDefaults.cs`; edit that file to change them. The first time you pick the CSS Unreal Engine option it will walk you through GitHub device-flow auth if you've set the client ID in AppDefaults. If that hits a 403 you can fall back to a PAT (set in AppDefaults or env SMEH_GITHUB_PAT, or enter when prompted).

Temp files end up under `%TEMP%\SMEH`. Option 6 clears those.
