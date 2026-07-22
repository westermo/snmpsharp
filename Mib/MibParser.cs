

using System;
using System.Collections.Generic;
using Parlot;
using Parlot.Fluent;
using SnmpSharpNet.Mib.Ast;

namespace SnmpSharpNet.Mib;

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
}