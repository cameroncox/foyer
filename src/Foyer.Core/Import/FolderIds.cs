namespace Foyer.Core.Import;

/// <summary>Hands out f1, f2, … in document order, so the same file always gives the same ids.</summary>
internal sealed class FolderIds
{
    private int _next;

    public string Next() => $"f{++_next}";
}
