# SMEH

SMEH (Satisfactory Modding Environment Helper) is a small console app for setting up the Satisfactory modding environment on Windows. It installs Visual Studio 2022, Clang, the CSS Unreal Engine, the SML starter project, and Wwise, and can generate project files and build the editor.

Build with `dotnet build` and run the exe, or use `dotnet run`. Temp files go under `%TEMP%\SMEH` — use option 9 (Cleanup) to remove them.

## Menu (1–11)

1. **Run all setup steps** - Setup in order. You set paths once at the start.
2. **CSS Unreal Engine** - Download and install the custom CSS Unreal Engine.
3. **Visual Studio 2022** - VS 2022 Community with the SML Configuration.
4. **Clang** - Cross-toolchain Unreal needs.
5. **Starter Project** - Clone the SatisfactoryModLoader repo.
6. **Wwise** - Wwise-CLI and integration into the starter project.
7. **Generate Visual Studio project files** - Generate .sln.
8. **Build Editor** - Development Editor. After a successful build, SMEH can open the next setup step in the FICSIT docs.
9. **Cleanup** - Deletes SMEH temp files under `%TEMP%\SMEH`.
10. **Open Docs** - Opens the FICSIT docs in your default browser.
11. **Open Modding Discord** - Opens the FICSIT modding Discord in your default browser.
0. **Exit**

Steps are in dependency order.
