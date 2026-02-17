namespace SMEH.Services;

public class CleanupService
{
    /// <summary>Root temp directory used by the app (VS2022, Clang, CssUnrealEngine, WwiseCLI).</summary>
    public static string TempRoot => Path.Combine(Path.GetTempPath(), "SMEH");

    public Task RunAsync()
    {
        var path = TempRoot;
        if (!Directory.Exists(path))
        {
            Console.WriteLine("No SMEH temp files found. Nothing to clean.");
            return Task.CompletedTask;
        }

        Console.WriteLine("The following directory and all its contents will be deleted:");
        Console.WriteLine($"  {path}");
        Console.WriteLine();
        Console.Write("Are you sure? (y/n): ");
        var answer = Console.ReadLine()?.Trim().ToUpperInvariant();
        if (answer != "Y" && answer != "YES")
        {
            Console.WriteLine("Cleanup cancelled.");
            return Task.CompletedTask;
        }

        try
        {
            Directory.Delete(path, recursive: true);
            Console.WriteLine("Temp files deleted successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not delete all temp files: {ex.Message}");
            Console.WriteLine("Some files may be in use. Close other programs or try again later.");
        }

        return Task.CompletedTask;
    }
}
