namespace KestrelAIProxy;

public static class PipelineExtensions
{
    public static IApplicationBuilder UsePipelineRouter(this IApplicationBuilder app)
    {
        return app.UseMiddleware<PipelineRouterMiddleware>();
    }

    public static IApplicationBuilder UseStaticPipeline(this IApplicationBuilder app,
        Action<IApplicationBuilder> configure)
    {
        return app.UseWhen(context =>
                context.Items.TryGetValue("PipelineType", out var pipelineType) &&
                pipelineType is PipelineType.Static,
            configure);
    }

    public static IApplicationBuilder UseGatewayPipeline(this IApplicationBuilder app,
        Action<IApplicationBuilder> configure)
    {
        return app.UseWhen(context =>
                context.Items.TryGetValue("PipelineType", out var pipelineType) &&
                pipelineType is PipelineType.Gateway,
            configure);
    }
}