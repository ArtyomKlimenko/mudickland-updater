namespace MuDickLand.Updater;

public static class PathSafety
{
    public static string NormalizeManifestPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("Manifest contains an empty path.");
        }

        var cleaned = path.Replace('\\', '/').Trim();
        if (cleaned.StartsWith('/') || cleaned.Contains(':'))
        {
            throw new InvalidOperationException("Manifest contains an absolute path: " + path);
        }

        var parts = cleaned.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            throw new InvalidOperationException("Manifest contains an empty path.");
        }

        foreach (var part in parts)
        {
            if (part is "." or "..")
            {
                throw new InvalidOperationException("Manifest contains path traversal: " + path);
            }

            if (part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new InvalidOperationException("Manifest contains invalid file name: " + path);
            }
        }

        return string.Join('/', parts);
    }

    public static string CombineUnderRoot(string root, string normalizedManifestPath)
    {
        var rootFull = Path.GetFullPath(root);
        var targetFull = Path.GetFullPath(Path.Combine(
            rootFull,
            normalizedManifestPath.Replace('/', Path.DirectorySeparatorChar)));

        if (!IsUnderRoot(rootFull, targetFull))
        {
            throw new InvalidOperationException("Target path escapes install directory: " + normalizedManifestPath);
        }

        return targetFull;
    }

    public static bool IsUnderRoot(string rootFull, string targetFull)
    {
        rootFull = Path.GetFullPath(rootFull).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        targetFull = Path.GetFullPath(targetFull);
        return targetFull.Equals(rootFull, StringComparison.OrdinalIgnoreCase)
            || targetFull.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || targetFull.StartsWith(rootFull + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    public static void EnsureNoReparsePointInExistingPath(string root, string target)
    {
        var rootFull = Path.GetFullPath(root);
        var current = Path.GetFullPath(target);

        while (!string.IsNullOrEmpty(current) && IsUnderRoot(rootFull, current))
        {
            if ((File.Exists(current) || Directory.Exists(current))
                && File.GetAttributes(current).HasFlag(FileAttributes.ReparsePoint))
            {
                throw new InvalidOperationException("Refusing to write through a reparse point: " + current);
            }

            if (current.Equals(rootFull, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            current = Path.GetDirectoryName(current) ?? "";
        }
    }

    public static bool IsUnderManagedDir(string normalizedPath, HashSet<string> managedDirs)
    {
        var first = normalizedPath.Split('/')[0];
        return managedDirs.Contains(first);
    }
}

