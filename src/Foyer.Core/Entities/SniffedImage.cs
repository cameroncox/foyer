namespace Foyer.Core.Entities;

/// <summary>An image type recognised from file content.</summary>
public sealed record SniffedImage(string ContentType, string Extension);
