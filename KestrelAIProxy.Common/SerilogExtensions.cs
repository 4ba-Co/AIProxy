using Microsoft.Extensions.DependencyInjection;

using Serilog;
using Serilog.Templates;
using Serilog.Templates.Themes;

namespace KestrelAIProxy.Common;

public static class SerilogExtensions
{
    public static void UseSerilog(this IServiceCollection sc)
    {
        sc.AddSerilog((services, lc) => lc
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .WriteTo.Console(new ExpressionTemplate(
                // Include trace and span ids when present.
                "[{@t:HH:mm:ss} {@l:u3}{#if @tr is not null} ({substring(@tr,0,4)}:{substring(@sp,0,4)}){#end}] {@m}\n{@x}",
                theme: TemplateTheme.Code))
            .MinimumLevel.Information());
    }
}