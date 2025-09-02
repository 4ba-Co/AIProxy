using Microsoft.Extensions.DependencyInjection;

using Serilog;
using Serilog.Events;
using Serilog.Templates;
using Serilog.Templates.Themes;

namespace KestrelAIProxy.Common;

public static class SerilogExtensions
{
    public static void UseSerilog(this IServiceCollection sc, string dsn)
    {
        sc.AddSerilog((services, lc) => lc
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .MinimumLevel.Debug()
            .WriteTo.Console(new ExpressionTemplate(
                // Include trace and span ids when present.
                "[{@t:HH:mm:ss} {@l:u3}{#if @tr is not null} ({substring(@tr,0,4)}:{substring(@sp,0,4)}){#end}] {@m}\n{@x}",
                theme: TemplateTheme.Code))
            .WriteTo.Sentry(s =>
            {
                s.Dsn = dsn;
                s.MinimumBreadcrumbLevel = LogEventLevel.Debug;
                s.MinimumEventLevel = LogEventLevel.Warning;
            }));
    }
}