namespace LLMEval;

/// <summary>
/// Resolves where suite reports should be written. CI sets <c>LLMEVAL_REPORT_DIR</c>
/// (e.g. <c>artifacts/llmeval</c>) so pipelines can upload HTML/JSON/MD/CSV artifacts.
/// </summary>
public static class ReportPaths
{
    public const string ReportDirectoryEnvironmentVariable = "LLMEVAL_REPORT_DIR";

    /// <summary>
    /// Returns <c>LLMEVAL_REPORT_DIR</c> when set; otherwise <paramref name="fallbackDirectory"/>.
    /// Relative paths are resolved against <c>GITHUB_WORKSPACE</c>, Azure DevOps source directory,
    /// or the process current directory — not the test assembly bin folder.
    /// </summary>
    public static string ResolveReportDirectory(string fallbackDirectory)
    {
        var fromEnv = Environment.GetEnvironmentVariable(ReportDirectoryEnvironmentVariable);
        var path = string.IsNullOrWhiteSpace(fromEnv) ? fallbackDirectory : fromEnv.Trim();
        if (Path.IsPathRooted(path))
            return path;

        var workspace = Environment.GetEnvironmentVariable("GITHUB_WORKSPACE")
                        ?? Environment.GetEnvironmentVariable("BUILD_SOURCESDIRECTORY")
                        ?? Environment.GetEnvironmentVariable("SYSTEM_DEFAULTWORKINGDIRECTORY");

        return string.IsNullOrWhiteSpace(workspace)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(workspace, path));
    }
}
