using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

static class ResultExtensions
{    
    extension<T>(Result<T> result)
        where T : IEquatable<T>
    {
        public Result<U> Select<U>(Func<T, U> selector)
            where U : IEquatable<U>
        {
            if (result.IsOk)
            {
                return selector(result.Value);
            }

            return result.Diagnostic;
        }
    }

    extension<T>(IEnumerable<Result<T>> results)
        where T : IEquatable<T>
    {
        public IEnumerable<T> ReportAll(SourceProductionContext ctx)
        {
            foreach (var result in results)
            {
                if (result.OrReport(ctx, out var value))
                {
                    yield return value;
                }
            }
        }
    }
}
public abstract class Result<T> : IEquatable<Result<T>>
    where T : IEquatable<T>
{
    public abstract bool IsOk { get; }
    public abstract T Value { get; }
    public abstract Diagnostic Diagnostic { get; }

    public abstract bool OrReport(
        SourceProductionContext ctx,
        [MaybeNullWhen(false)] out T value
    );

    public abstract bool Equals(Result<T>? other);

    public static implicit operator Result<T>(T value) => new Ok<T>(value);
    public static implicit operator Result<T>(Diagnostic diag) => new Error<T>(diag);
}

public class Ok<T>(T inner) : Result<T>
    where T : IEquatable<T>
{
    public override bool IsOk => true;
    public override T Value => inner;
    public override Diagnostic Diagnostic => throw new InvalidOperationException("Can't access .Diagnostic on Ok<T>");

    public override bool OrReport(
        SourceProductionContext ctx,
        [MaybeNullWhen(false)] out T value
    ) {
        value = inner;
        return true;
    }

    public override bool Equals(Result<T>? other)
    {
        return other is Ok<T> ok && inner.Equals(ok.Value);
    }
}

public class Error<T>(Diagnostic diag) : Result<T>
    where T : IEquatable<T>
{
    public override bool IsOk => false;
    public override T Value => throw new InvalidOperationException("Can't access .Value on Error<T>");
    public override Diagnostic Diagnostic => diag;

    public override bool OrReport(
        SourceProductionContext ctx,
        [MaybeNullWhen(false)] out T value
    ) {
        ctx.ReportDiagnostic(diag);
        value = default;
        return false;
    }

    public override bool Equals(Result<T>? other)
    {
        return other is Error<T> error && diag.Equals(error.Diagnostic);
    }
}
