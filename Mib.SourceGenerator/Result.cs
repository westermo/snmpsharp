using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

static class ResultExtensions
{    
    extension<T>(IEnumerable<Result<T>> results)
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
internal abstract class Result<T>
{
    public abstract bool OrReport(
        SourceProductionContext ctx,
        [MaybeNullWhen(false)] out T value
    );

    public static implicit operator Result<T>(T value) => new Ok<T>(value);
    public static implicit operator Result<T>(Diagnostic diag) => new Error<T>(diag);
}

internal class Ok<T>(T inner) : Result<T>
{
    public override bool OrReport(
        SourceProductionContext ctx,
        [MaybeNullWhen(false)] out T value
    ) {
        value = inner;
        return true;
    }


}

internal class Error<T>(Diagnostic diag) : Result<T>
{
    public override bool OrReport(
        SourceProductionContext ctx,
        [MaybeNullWhen(false)] out T value
    ) {
        ctx.ReportDiagnostic(diag);
        value = default;
        return false;
    }
}