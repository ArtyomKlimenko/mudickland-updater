using MuDickLand.Updater;

var tests = new (string Name, Action Body)[]
{
    ("Transport policy accepts HTTPS", () => TransportPolicy.RequireAllowedHttpUri("https://example.test/latest.json", "latestUrl")),
    ("Transport policy accepts localhost HTTP", () => TransportPolicy.RequireAllowedHttpUri("http://127.0.0.1:8088/latest.json", "latestUrl")),
    ("Transport policy rejects production HTTP", () => AssertThrows(() => TransportPolicy.RequireAllowedHttpUri("http://example.test/latest.json", "latestUrl"))),
    ("Path safety normalizes slash style", () => AssertEqual("mods/a.jar", PathSafety.NormalizeManifestPath(@"mods\a.jar"))),
    ("Path safety rejects traversal", () => AssertThrows(() => PathSafety.NormalizeManifestPath("../mods/a.jar"))),
    ("Path safety rejects absolute Windows path", () => AssertThrows(() => PathSafety.NormalizeManifestPath(@"C:\tmp\a.jar"))),
    ("Path safety combines under root", () => AssertEqual(Path.GetFullPath(Path.Combine(Path.GetTempPath(), "mdl", "mods", "a.jar")), PathSafety.CombineUnderRoot(Path.Combine(Path.GetTempPath(), "mdl"), "mods/a.jar"))),
    ("Manifest dir check allows managed path", () => AssertTrue(PathSafety.IsUnderManagedDir("mods/a.jar", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "mods" }))),
    ("Manifest dir check rejects unmanaged path", () => AssertTrue(!PathSafety.IsUnderManagedDir("saves/world/level.dat", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "mods" }))),
};

var passed = 0;
foreach (var test in tests)
{
    test.Body();
    Console.WriteLine("PASS " + test.Name);
    passed++;
}

Console.WriteLine($"Passed {passed} updater core tests.");

static void AssertThrows(Action action)
{
    try
    {
        action();
    }
    catch
    {
        return;
    }

    throw new InvalidOperationException("Expected exception was not thrown.");
}

static void AssertEqual(string expected, string actual)
{
    if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
    }
}

static void AssertTrue(bool condition)
{
    if (!condition)
    {
        throw new InvalidOperationException("Expected condition to be true.");
    }
}

