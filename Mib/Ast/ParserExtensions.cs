//
// This is a hack to get better errors
//

using Parlot.Fluent;
using Parlot.Compilation;
using Parlot.Rewriting;
using System;
using Parlot;

namespace Mib.Ast;

public static class ParserExtension
{
    extension <T>(Parser<T> parser)
    {
        public Parser<T> WithName(string name)
        {
            return new Named<T>(parser, name);
        }
    }
}

interface INamedParser
{
    public string? Name { get; }
}

public sealed class Named<T> : Parser<T>, ICompilable, ISeekable, INamedParser
{
    private readonly Parser<T> _parser;

    public Named(Parser<T> parser, string name)
    {
        Name = name;
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));

        if (parser is ISeekable seekable)
        {
            CanSeek = seekable.CanSeek;
            ExpectedChars = seekable.ExpectedChars;
            SkipWhitespace = seekable.SkipWhitespace;
        }
    }

    public bool CanSeek { get; }

    public char[] ExpectedChars { get; } = [];

    public bool SkipWhitespace { get; }

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        var parsed = new ParseResult<T>();

        if (_parser.Parse(context, ref parsed))
        {
            result.Set(parsed.Start, parsed.End, parsed.Value);
            context.ExitParser(this);
            return true;
        }

        context.ExitParser(this);
        return false;
    }

    public CompilationResult Compile(CompilationContext context)
    {
        return _parser.Build(context, requireResult: true);
    }

    string? INamedParser.Name => Name;
    override public string ToString() => $"{Name}";
}