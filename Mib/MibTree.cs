using System;
using System.Collections.Generic;
using System.Linq;
using SnmpSharpNet.Mib.Ast;

namespace SnmpSharpNet.Mib;

/// <summary>
/// A node in the merged OID tree of one or more resolved <see cref="MibModule"/>s.
/// The root node returned by <see cref="MibTree.Build(IEnumerable{MibModule}, IEnumerable{string})"/> is synthetic
/// (it has no arc of its own) and its children are the top-level arcs (itu-t, iso, joint-iso-itu-t).
/// </summary>
public sealed class MibTreeNode
{
    private readonly Dictionary<uint, MibTreeNode> children = [];
    private uint[]? oid;

    internal MibTreeNode(uint arc, MibTreeNode? parent)
    {
        Arc = arc;
        Parent = parent;
    }

    /// <summary>The OID arc of this node relative to its parent. Always 0 for the root.</summary>
    public uint Arc { get; }

    /// <summary>The parent node, or <c>null</c> for the root.</summary>
    public MibTreeNode? Parent { get; }

    /// <summary><c>true</c> for the synthetic root node.</summary>
    public bool IsRoot => Parent is null;

    /// <summary>The MIB descriptor of this node, if any module names it.</summary>
    public string? Name { get; private set; }

    /// <summary>The identifier of the module that first defined this node.</summary>
    public string? ModuleName { get; private set; }

    /// <summary>The resolved MIB item for this node, if any.</summary>
    public MibItem? Item { get; private set; }

    /// <summary>Child nodes keyed by arc.</summary>
    public IReadOnlyDictionary<uint, MibTreeNode> Children => children;

    /// <summary>Child nodes ordered by arc.</summary>
    public IEnumerable<MibTreeNode> OrderedChildren => children.Values.OrderBy(child => child.Arc);

    /// <summary>The full OID of this node (empty for the root).</summary>
    public IReadOnlyList<uint> Oid => oid ??= AncestorsAndSelf().Skip(1).Select(node => node.Arc).ToArray();

    /// <summary>The OID in dotted-decimal form (empty for the root).</summary>
    public string DottedOid => string.Join(".", Oid);

    public bool IsNotification => Item is MibNotification;
    public bool IsTable => Item is MibTable;
    public bool IsLeaf => Item is MibLeaf;

    /// <summary><c>true</c> when the node carries a value-bearing item (leaf, table or notification).</summary>
    public bool IsValue => Item is MibLeaf or MibTable or MibNotification;

    /// <summary>All nodes from the root down to (and including) this node.</summary>
    public IEnumerable<MibTreeNode> AncestorsAndSelf()
    {
        var stack = new Stack<MibTreeNode>();
        for (var node = this; node is not null; node = node.Parent)
        {
            stack.Push(node);
        }

        return stack;
    }

    /// <summary>All descendants in depth-first, arc-ordered traversal (excluding this node).</summary>
    public IEnumerable<MibTreeNode> Descendants()
    {
        foreach (var child in OrderedChildren)
        {
            yield return child;
            foreach (var descendant in child.Descendants())
            {
                yield return descendant;
            }
        }
    }

    /// <summary>Finds the node at <paramref name="oid"/>, relative to this node.</summary>
    public bool TryFind(IEnumerable<uint> oid, out MibTreeNode node)
    {
        node = this;
        foreach (var arc in oid)
        {
            if (!node.children.TryGetValue(arc, out var child))
            {
                return false;
            }

            node = child;
        }

        return true;
    }

    /// <summary>Finds the deepest existing node along <paramref name="oid"/>, relative to this node.</summary>
    public MibTreeNode FindClosest(IEnumerable<uint> oid)
    {
        var node = this;
        foreach (var arc in oid)
        {
            if (!node.children.TryGetValue(arc, out var child))
            {
                break;
            }

            node = child;
        }

        return node;
    }

    public override string ToString() => IsRoot ? "<root>" : $"{Name ?? Arc.ToString()} ({DottedOid})";

    internal MibTreeNode GetOrAddChild(uint arc)
    {
        if (!children.TryGetValue(arc, out var child))
        {
            child = new MibTreeNode(arc, this);
            children.Add(arc, child);
        }

        return child;
    }

    internal void Define(string? name, string moduleName, MibItem? item, bool overwriteItem = true)
    {
        // Names like "<SNMPv2-SMI>" are placeholders for arcs of imported modules, not real names
        if (Name is null && !string.IsNullOrWhiteSpace(name) && !name!.StartsWith("<", StringComparison.Ordinal))
        {
            Name = name;
        }

        ModuleName ??= moduleName;

        if (item is not null && (overwriteItem || Item is null))
        {
            Item = item;
        }
    }
}

/// <summary>Builds a merged OID tree from resolved MIB modules.</summary>
public static class MibTree
{
    /// <summary>
    /// Builds the OID tree of <paramref name="modules"/>, optionally including the named
    /// <paramref name="builtinModules"/> (see <see cref="GetBuiltinDependencies(IEnumerable{ModuleDefinition})"/>).
    /// </summary>
    public static MibTreeNode Build(IEnumerable<MibModule> modules, IEnumerable<string>? builtinModules = null) =>
        Build(modules.Select(module => new KeyValuePair<string, MibModule>(module.Identifier, module)), builtinModules);

    /// <inheritdoc cref="Build(IEnumerable{MibModule}, IEnumerable{string})"/>
    public static MibTreeNode Build(
        IEnumerable<KeyValuePair<string, MibModule>> modules,
        IEnumerable<string>? builtinModules = null)
    {
        var root = new MibTreeNode(0, null);

        foreach (var moduleEntry in modules)
        {
            var moduleName = moduleEntry.Key;
            var module = moduleEntry.Value;
            foreach (var oidEntry in module.AllOids)
            {
                var ident = oidEntry.Value;
                module.AllItems.TryGetValue(ident, out var item);
                Insert(root, ident, oidEntry.Key, moduleName, item);
            }

            // Table columns are removed from MibModule.Items, but they still belong in the tree.
            foreach (var itemEntry in module.AllItems)
            {
                Insert(root, itemEntry.Key, itemEntry.Value.Ident.Name, moduleName, itemEntry.Value);
            }
        }

        // Builtin modules go last so they never override definitions from supplied modules
        foreach (var builtinName in builtinModules ?? [])
        {
            if (!BuiltinMib.All.TryGetValue(builtinName, out var exports) || exports is not BuiltinMib builtin)
            {
                continue;
            }

            foreach (var itemEntry in builtin.Items)
            {
                Insert(root, itemEntry.Key, itemEntry.Key.Name, builtinName, itemEntry.Value, overwriteItem: false);
            }
        }

        return root;
    }

    /// <summary>
    /// Returns the builtin modules (such as SNMPv2-SMI) imported by <paramref name="modules"/> but not supplied
    /// by them, including the builtin modules' own dependencies, ordered by name. Modules are keyed by identifier.
    /// </summary>
    public static IReadOnlyList<string> GetBuiltinDependencies(IEnumerable<ModuleDefinition> modules)
    {
        var byName = new Dictionary<string, ModuleDefinition>(StringComparer.Ordinal);
        foreach (var module in modules)
        {
            byName[module.Identifier.ToString()] = module;
        }

        return GetBuiltinDependencies(byName);
    }

    /// <summary>
    /// Returns the builtin modules imported by the supplied <paramref name="modules"/> (keyed by module name)
    /// but not supplied by them, including the builtin modules' own dependencies, ordered by name.
    /// </summary>
    public static IReadOnlyList<string> GetBuiltinDependencies(IReadOnlyDictionary<string, ModuleDefinition> modules)
    {
        var builtins = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<string>(modules.Values.SelectMany(module => module.Dependencies));
        while (pending.Count > 0)
        {
            var dependency = pending.Dequeue();
            if (modules.ContainsKey(dependency) || !BuiltinMib.All.TryGetValue(dependency, out var exports)
                                                || !builtins.Add(dependency))
            {
                continue;
            }

            if (exports is BuiltinMib builtin)
            {
                foreach (var transitive in builtin.Dependencies)
                {
                    pending.Enqueue(transitive);
                }
            }
        }

        return builtins.OrderBy(name => name, StringComparer.Ordinal).ToArray();
    }

    private static void Insert(
        MibTreeNode root,
        MibItemIdent ident,
        string? finalName,
        string moduleName,
        MibItem? item,
        bool overwriteItem = true)
    {
        var namesOffset = ident.Oid.Length - ident.OidNames.Length;
        var node = root;

        for (var index = 0; index < ident.Oid.Length; index++)
        {
            node = node.GetOrAddChild(ident.Oid[index]);
            var name = index >= namesOffset ? ident.OidNames[index - namesOffset] : null;
            if (index == ident.Oid.Length - 1 && !string.IsNullOrWhiteSpace(finalName))
            {
                name = finalName;
            }

            node.Define(name, moduleName, index == ident.Oid.Length - 1 ? item : null, overwriteItem);
        }
    }
}
