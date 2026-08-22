namespace Canary.Core.Init;

public sealed class InitResult
{
    // True if SiteInitializer.Initialize refused to touch the target
    // directory (a canary.json already exists there and force wasn't
    // passed). When true, RefusalMessage is set and no files were written.
    public bool Refused { get; init; }
    public string? RefusalMessage { get; init; }

    public List<string> FilesWritten { get; } = new();
    public List<string> Warnings { get; } = new();
}
