using Polly;
using Polly.Registry;

namespace DeckFlow.Web.Tests.TestDoubles;

/// <summary>Runs a deterministic hook after the HTTP callback has completed.</summary>
internal sealed class AfterResponsePipelineProvider(Action afterResponse) : ResiliencePipelineProvider<string>
{
    public override ResiliencePipeline<T> GetPipeline<T>(string key)
        => new ResiliencePipelineBuilder<T>().AddStrategy(_ => new AfterResponseStrategy(afterResponse)).Build();

    public override bool TryGetPipeline<T>(string key, out ResiliencePipeline<T> pipeline)
    {
        pipeline = GetPipeline<T>(key);
        return true;
    }

    public override bool TryGetPipeline(string key, out ResiliencePipeline pipeline)
    {
        pipeline = ResiliencePipeline.Empty;
        return true;
    }

    private sealed class AfterResponseStrategy(Action afterResponse) : ResilienceStrategy
    {
        protected override async ValueTask<Outcome<TResult>> ExecuteCore<TResult, TState>(
            Func<ResilienceContext, TState, ValueTask<Outcome<TResult>>> callback,
            ResilienceContext context, TState state)
        {
            Outcome<TResult> outcome = await callback(context, state);
            if (outcome.Exception is null && outcome.Result is RestSharp.RestResponse { IsSuccessful: true }) afterResponse();
            return outcome;
        }
    }
}
