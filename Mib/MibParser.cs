

using System;
using System.Collections.Generic;
using System.Linq;
using Parlot;
using Parlot.Fluent;
using SnmpSharpNet.Mib.Ast;

namespace SnmpSharpNet.Mib;

public class MibConstructionException(string moduleName, Exception innerException)
    : Exception($"Failed to construct MIB module '{moduleName}'", innerException)
{
    public string ModuleName => moduleName;
}

public static class MibParser {
    public static ModuleDefinition ParseAst(string contents, string moduleHint = "?")
    {

        var context = new ParseContext(new Scanner(contents))
        {
            WhiteSpaceParser = SMIv2.MibWhiteSpace
        };


        var parseStack = new Stack<string>();
        context.OnEnterParser += (parser, ctx) =>
        {
            //var s = parser is INamedParser p ? p.Name : "wtf";
            parseStack.Push(parser.ToString());
        };
        context.OnExitParser += (parser, ctx) => parseStack.Pop();
        
        ModuleDefinition.Module.Compile().TryParse(context, out var result, out var maybeError);
        if (maybeError is {} error)
        {
            throw new ParseException(
                $"""
                Failed to parse MIB module '{moduleHint}' @ {error!.Position}
                    Error: {error!.Message}
                    Stack:
                        {string.Join(",        \n", parseStack)}
                    Next: {context.Scanner.Cursor.Span.Slice(0, 32).ToString().Replace("\n", "\\n")}
                """,
                error.Position
            );
        }

        return result;
    }

    public static MibModule ParseModule(
        string contents, string moduleHint = "?", 
        IReadOnlyDictionary<string, IMibThatExports> importables = null!
    ) {
        return new MibModule(ParseAst(contents, moduleHint), importables ?? BuiltinMib.All);
    }


    public static Dictionary<string, MibModule> ParseModules(
        IReadOnlyDictionary<string, ModuleDefinition> asts
    ) {
        var orderedAsts = TopologicalSort(
            asts,
            mod => mod.Dependencies.Where(dep => !BuiltinMib.All.ContainsKey(dep))
        );

        var cache = new Dictionary<string, IMibThatExports>((Dictionary<string, IMibThatExports>)BuiltinMib.All);
        foreach (var mod in orderedAsts)
        {
            var name = mod.Identifier.ToString();
            try
            {
                cache[name] = new MibModule(mod, cache);           
            }
            catch (Exception e)
            {
                throw new MibConstructionException(name, e);
            }
        }
        
        return cache
            .Where(kvp => kvp.Value is MibModule && asts.ContainsKey(kvp.Key))
            .ToDictionary(kvp => kvp.Key, kvp => (MibModule)kvp.Value);
    }

    private static IEnumerable<ModuleDefinition> TopologicalSort(
        IReadOnlyDictionary<string, ModuleDefinition> asts,
        Func<ModuleDefinition, IEnumerable<string>> getDeps
    ) {
        var visited = new HashSet<string>();
        var stack = new HashSet<string>();
        var result = new List<ModuleDefinition>();

        void Visit(string key)
        {
            if (visited.Contains(key)) { return; }
            if (!stack.Add(key))
            {
                throw new InvalidOperationException($"Module '{key}' has a circular dependency");
            }
            if (!asts.TryGetValue(key, out var module))
            {
                throw new KeyNotFoundException($"Module '{key}' is not found");
            }
            foreach (var dep in getDeps(module))
            {
                Visit(dep);
            }
            stack.Remove(key);
            visited.Add(key);
            result.Add(module);
        }

        foreach (var key in asts.Keys)
        {
            Visit(key);
        }

        return result;
    }
}