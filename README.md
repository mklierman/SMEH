# SMEH

SMEH (Satisfactory Modding Environment Helper) is a small console app for setting up the Satisfactory modding environment on Windows. It installs Visual Studio 2022, Clang, the CSS Unreal Engine, the SML starter project, and Wwise, and can generate project files, build the editor, and open the project.

Build with `dotnet build` and run the exe, or use `dotnet run`. Temp files go under `%TEMP%\SMEH` — use option 10 (Cleanup) to remove them.

## Menu (1–10)

1. **Run all** — Unattended setup in order; you set paths once at the start.
2. **CSS Unreal Engine** — Download and install the Custom Satisfactory Unreal Engine.
3. **Visual Studio 2022** — VS 2022 Community with the SML Configuration.
4. **Clang** — Cross-toolchain Unreal needs.
5. **Starter Project** — Clone the SatisfactoryModLoader repo.
6. **Wwise** — Wwise-CLI and integration into the starter project.
7. **Generate Visual Studio project files** — UnrealBuildTool for .sln/.vcxproj.
8. **Build Editor** — FactoryEditor, Development Editor, Win64.
9. **Open in Unreal Editor** — Opens FactoryGame.uproject.
10. **Cleanup** — Deletes SMEH temp files under `%TEMP%\SMEH`.
0. **Exit**

Steps are in dependency order.
