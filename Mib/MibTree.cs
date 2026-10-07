using System;
using System.Collections.Generic;
using System.Linq;

namespace SnmpSharpNet.Mib;

/// <summary>
/// A node in the merged OID tree of one or more resolved <see cref="MibModule"/>s.
/// The root node returned by <see cref="MibTree.Build(IEnumerable{MibModule})"/> is synthetic
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

    internal void Define(string? name, string moduleName, MibItem? item)
    {
        if (Name is null && !string.IsNullOrWhiteSpace(name))
        {
            Name = name;
        }

        ModuleName ??= moduleName;

        if (item is not null)
        {
            Item = item;
        }
    }
}

/// <summary>Builds a merged OID tree from resolved MIB modules.</summary>
public static class MibTree
{
    public static MibTreeNode Build(IEnumerable<MibModule> modules) =>
        Build(modules.Select(module => new KeyValuePair<string, MibModule>(module.Identifier, module)));

    public static MibTreeNode Build(IEnumerable<KeyValuePair<string, MibModule>> modules)
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

        return root;
    }

    private static void Insert(
        MibTreeNode root,
        MibItemIdent ident,
        string? finalName,
        string moduleName,
        MibItem? item)
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

            node.Define(name, moduleName, index == ident.Oid.Length - 1 ? item : null);
        }
    }
}
