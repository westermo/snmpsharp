namespace SnmpSharpNet.Mib.Tests;

/// <summary>
/// Verifies that the core SMI modules (SNMPv2-SMI, SNMPv2-TC, SNMPv2-CONF), which are normally
/// provided by <see cref="BuiltinMib"/>, can also be supplied as MIB files.
/// </summary>
public class CoreModuleTests
{
    private static Dictionary<string, Ast.ModuleDefinition> LoadCore()
    {
        var asts = new Dictionary<string, Ast.ModuleDefinition>();
        foreach (var mibFile in new[] { "SNMPv2-SMI.mib", "SNMPv2-TC.mib", "SNMPv2-CONF.mib" })
        {
            var ast = MibParser.ParseAst(File.ReadAllText(mibFile), mibFile);
            asts[ast.Identifier.ToString()] = ast;
        }

        return asts;
    }

    [Test]
    public async Task SnmpV2Smi_ExportsApplicationTypesNodesAndMacros()
    {
        var smi = MibParser.ParseModules(LoadCore())["SNMPv2-SMI"];

        foreach (var (name, kind) in new[]
                 {
                     ("Integer32", TypeKind.Integer32),
                     ("IpAddress", TypeKind.IpAddress),
                     ("Counter32", TypeKind.Counter32),
                     ("Gauge32", TypeKind.Gauge32),
                     ("Unsigned32", TypeKind.Unsigned32),
                     ("TimeTicks", TypeKind.TimeTicks),
                     ("Opaque", TypeKind.Opaque),
                     ("Counter64", TypeKind.Counter64),
                     ("ObjectName", TypeKind.ObjectIdentifier),
                 })
        {
            await Assert.That(smi.TryImport(name, out _, out var type)).IsTrue().Because(name);
            await Assert.That(type?.Kind).IsEqualTo(kind).Because(name);
        }

        await Assert.That(smi.TryImport("enterprises", out var enterprises, out _)).IsTrue();
        await Assert.That(enterprises!.Ident.Oid).IsEquivalentTo(new uint[] { 1, 3, 6, 1, 4, 1 });
        await Assert.That(smi.TryImport("zeroDotZero", out var zeroDotZero, out _)).IsTrue();
        await Assert.That(zeroDotZero!.Ident.Oid).IsEquivalentTo(new uint[] { 0, 0 });

        foreach (var keyword in new[] { "MODULE-IDENTITY", "OBJECT-TYPE", "NOTIFICATION-TYPE", "ObjectSyntax" })
        {
            await Assert.That(smi.TryImport(keyword, out _, out _)).IsTrue().Because(keyword);
        }
    }

    [Test]
    public async Task SnmpV2Tc_ResolvesTextualConventionsAgainstSuppliedSmi()
    {
        var tc = MibParser.ParseModules(LoadCore())["SNMPv2-TC"];

        await Assert.That(tc.TryImport("TEXTUAL-CONVENTION", out _, out _)).IsTrue();
        await Assert.That(tc.TryImport("DisplayString", out _, out var displayString)).IsTrue();
        await Assert.That(displayString?.Kind).IsEqualTo(TypeKind.OctetString);
        await Assert.That(tc.TryImport("RowStatus", out _, out var rowStatus)).IsTrue();
        await Assert.That(rowStatus?.Values?["createAndGo"]).IsEqualTo(4);
    }

    [Test]
    public async Task DependentMib_ResolvesAgainstSuppliedCoreModules()
    {
        var asts = LoadCore();
        foreach (var mibFile in new[] { "IANAifType-MIB.mib", "IF-MIB.mib", "SNMPv2-MIB.mib" })
        {
            var ast = MibParser.ParseAst(File.ReadAllText(mibFile), mibFile);
            asts[ast.Identifier.ToString()] = ast;
        }

        var modules = MibParser.ParseModules(asts);

        await Assert.That(modules.Count).IsEqualTo(asts.Count);
        await Assert.That(modules["IF-MIB"].Items.Count).IsGreaterThan(0);
    }

    /// <summary>
    /// A supplied module that shares its name with a builtin must be constructed before the
    /// modules importing from it, otherwise imports resolve against the (smaller) builtin.
    /// </summary>
    [Test]
    public async Task SuppliedModuleShadowingBuiltin_IsOrderedBeforeItsDependents()
    {
        var asts = new Dictionary<string, Ast.ModuleDefinition>();
        foreach (var (name, source) in new[]
                 {
                     ("CONSUMER-MIB", """
                                      CONSUMER-MIB DEFINITIONS ::= BEGIN
                                      IMPORTS sysObjects FROM SNMPv2-MIB;
                                      consumer OBJECT IDENTIFIER ::= { sysObjects 1 }
                                      END
                                      """),
                     ("SNMPv2-MIB", """
                                    SNMPv2-MIB DEFINITIONS ::= BEGIN
                                    IMPORTS mib-2 FROM SNMPv2-SMI;
                                    sysObjects OBJECT IDENTIFIER ::= { mib-2 9999 }
                                    END
                                    """),
                 })
        {
            asts[name] = MibParser.ParseAst(source, name);
        }

        var modules = MibParser.ParseModules(asts);

        await Assert.That(modules["CONSUMER-MIB"].TryImport("consumer", out var consumer, out _)).IsTrue();
        await Assert.That(consumer!.Ident.Oid).IsEquivalentTo(new uint[] { 1, 3, 6, 1, 2, 1, 9999, 1 });
    }
}
