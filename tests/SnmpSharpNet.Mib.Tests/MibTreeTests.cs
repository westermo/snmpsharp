namespace SnmpSharpNet.Mib.Tests;

public class MibTreeTests
{
    private static readonly string MibDir = AppContext.BaseDirectory;

    private static IEnumerable<Ast.ModuleDefinition> LoadAll() =>
        Directory.GetFiles(MibDir, "*.mib").Select(MibParser.ParseAstFile);

    [Test]
    public async Task Build_ExposesNamedNodesWithItems()
    {
        var root = MibTree.Build(MibParser.ParseModules(LoadAll()).Values);

        await Assert.That(root.IsRoot).IsTrue();
        await Assert.That(root.Oid).IsEmpty();

        var found = root.TryFind([1, 3, 6, 1, 2, 1, 2, 2, 1, 1], out var ifIndex);
        await Assert.That(found).IsTrue();
        await Assert.That(ifIndex.Name).IsEqualTo("ifIndex");
        await Assert.That(ifIndex.ModuleName).IsEqualTo("IF-MIB");
        await Assert.That(ifIndex.Item).IsTypeOf<MibLeaf>();
        await Assert.That(ifIndex.DottedOid).IsEqualTo("1.3.6.1.2.1.2.2.1.1");
        await Assert.That(ifIndex.Parent!.Name).IsEqualTo("ifEntry");

        await Assert.That(root.TryFind([1, 3, 6, 1, 2, 1, 2, 2], out var ifTable)).IsTrue();
        await Assert.That(ifTable.IsTable).IsTrue();

        await Assert.That(root.TryFind([1, 3, 6, 1, 6, 3, 1, 1, 5, 4], out var linkUp)).IsTrue();
        await Assert.That(linkUp.IsNotification).IsTrue();
    }

    [Test]
    public async Task Build_ContainsEveryModuleItem()
    {
        var modules = MibParser.ParseModules(LoadAll());
        var root = MibTree.Build(modules);

        var missing = modules.Values
            .SelectMany(module => module.AllItems.Keys)
            .Where(ident => !root.TryFind(ident.Oid, out var node) || node.Item is null)
            .Select(ident => ident.ToString())
            .ToList();

        await Assert.That(missing).IsEmpty();
    }

    [Test]
    public async Task Descendants_AreDepthFirstAndArcOrdered()
    {
        var root = MibTree.Build(MibParser.ParseModules(LoadAll()).Values);

        var oids = root.Descendants().Select(node => node.Oid.ToArray()).ToList();
        var sorted = oids.OrderBy(oid => oid, OidComparer.Instance).ToList();

        await Assert.That(oids.Count).IsGreaterThan(100);
        await Assert.That(oids.SequenceEqual(sorted)).IsTrue();
    }

    [Test]
    public async Task FindClosest_ReturnsDeepestKnownNode()
    {
        var root = MibTree.Build(MibParser.ParseModules(LoadAll()).Values);

        var node = root.FindClosest([1, 3, 6, 1, 2, 1, 2, 2, 1, 1, 42]);

        await Assert.That(node.Name).IsEqualTo("ifIndex");
    }

    [Test]
    public async Task ParseModules_CombinesAstsAndLaterDuplicatesWin()
    {
        const string first = """
                             RUNTIME-TEST-MIB DEFINITIONS ::= BEGIN
                             IMPORTS ifIndex FROM IF-MIB;
                             runtimeRoot OBJECT IDENTIFIER ::= { iso 99 }
                             END
                             """;
        const string second = """
                              RUNTIME-TEST-MIB DEFINITIONS ::= BEGIN
                              IMPORTS ifIndex FROM IF-MIB;
                              runtimeReplaced OBJECT IDENTIFIER ::= { iso 98 }
                              END
                              """;

        var modules = MibParser.ParseModules(
            LoadAll()
                .Append(MibParser.ParseAst(first))
                .Append(MibParser.ParseAst(second)));

        await Assert.That(modules.ContainsKey("IF-MIB")).IsTrue();
        var runtime = modules["RUNTIME-TEST-MIB"];
        await Assert.That(runtime.AllOids.ContainsKey("runtimeReplaced")).IsTrue();
        await Assert.That(runtime.AllOids.ContainsKey("runtimeRoot")).IsFalse();

        var root = MibTree.Build(modules);
        await Assert.That(root.TryFind([1, 98], out var replaced)).IsTrue();
        await Assert.That(replaced.Name).IsEqualTo("runtimeReplaced");
        await Assert.That(root.TryFind([1, 99], out _)).IsFalse();
    }

    private sealed class OidComparer : IComparer<uint[]>
    {
        public static readonly OidComparer Instance = new();

        public int Compare(uint[]? x, uint[]? y)
        {
            for (var i = 0; i < Math.Min(x!.Length, y!.Length); i++)
            {
                var result = x[i].CompareTo(y[i]);
                if (result != 0) return result;
            }

            return x.Length.CompareTo(y.Length);
        }
    }
}
