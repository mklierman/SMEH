# SMEH

SMEH is a small console app that helps you set up a Satisfactory modding environment on Windows. It installs : Visual Studio 2022, the Clang toolchain, the CSS Unreal Engine build, the SML starter project, and Wwise. 

You need .NET 8 to run it. Build with `dotnet build` and run the exe, or use `dotnet run`. Copy `appsettings.json` to the output folder if you’re running from the build directory; the app uses it for URLs and options. The first time you pick the CSS Unreal Engine option it will walk you through GitHub device-flow auth if you’ve set the client ID. If that hits a 403 you can fall back to a PAT (from config or by entering it when prompted).

Temp files end up under `%TEMP%\SMEH`. Option 6 clears those. Option 7 clears the saved GitHub token so you can re-auth next time.