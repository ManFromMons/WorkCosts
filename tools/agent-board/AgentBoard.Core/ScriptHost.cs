using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AgentBoard;

public sealed class ScriptHost
{
    public string BashExecutable { get; }
    public string RepoRoot { get; }

    public ScriptHost(string repoRoot, string? bashExecutable = null)
    {
        RepoRoot = repoRoot;
        BashExecutable = bashExecutable ?? ResolveBash();
    }

    public static string ResolveBash()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "/bin/bash";
        }

        foreach (var candidate in new[]
                 {
                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Git", "bin", "bash.exe"),
                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Git", "bin", "bash.exe"),
                     "bash.exe"
                 })
        {
            if (File.Exists(candidate) || candidate == "bash.exe")
            {
                return candidate;
            }
        }

        return "bash.exe";
    }

    public IReadOnlyList<string> ArgvFor(string scriptFileName, IEnumerable<string>? extraArgs = null)
    {
        var script = Path.Combine(RepoRoot, "scripts", scriptFileName);
        var args = new List<string> { script };
        if (extraArgs is not null)
        {
            args.AddRange(extraArgs);
        }

        return args;
    }

    public bool CommandLineUsesCmdExe(string commandLine) =>
        commandLine.Contains("cmd.exe", StringComparison.OrdinalIgnoreCase);

    public static bool GitCatFileArgvIsSafe(IReadOnlyList<string> argv) =>
        argv.Count >= 3
        && argv[0] == "git"
        && argv[1] == "cat-file"
        && argv[2] == "-e"
        && argv.All(a => !a.Contains("cmd.exe", StringComparison.OrdinalIgnoreCase));

    public ProcessStartInfo StartGitCatFile(string revPath) =>
        new()
        {
            FileName = "git",
            ArgumentList = { "cat-file", "-e", revPath },
            WorkingDirectory = RepoRoot,
            UseShellExecute = false
        };
}

public sealed class InboxCloser
{
    public bool LandCalled { get; private set; }
    public string? LastScript { get; private set; }

    public string Close(bool dirty, Action<string> land)
    {
        if (!dirty)
        {
            return "dismiss";
        }

        LandCalled = true;
        LastScript = "update-to-review.sh";
        land(LastScript);
        return "land";
    }
}
