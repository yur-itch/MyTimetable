using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using MyTimetable.Planning;
using NUglify;

namespace MyTimetable
{
    // Готовый (отрендеренный + минифицированный) HTML страницы планирования. Список стратегий фиксирован
    // на старте, но очередь предметов меняется планированием и снятием конфликтов — поэтому блоб
    // пересобирается (Rebuild) после каждого изменения очереди, а GET /App/Plan лишь отдаёт текущий Html
    // без рендера и минификации на запрос. Зеркало CacheRebuilder, но для статической формы планирования.
    public sealed class PlanPage
    {
        private readonly ViewRenderer _renderer;
        private readonly IServiceProvider _serviceProvider;
        private readonly IReadOnlyCollection<string> _strategies;

        public string Html { get; private set; } = "";

        public PlanPage(ViewRenderer renderer, IServiceProvider serviceProvider,
                        IReadOnlyDictionary<string, IPlanningSelectorFactory> strategies)
        {
            _renderer = renderer;
            _serviceProvider = serviceProvider;
            _strategies = strategies.Keys.ToList();
        }

        public async Task Rebuild(IReadOnlyDictionary<string, int> queue)
        {
            using var scope = _serviceProvider.CreateScope();
            var httpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
            var routeData = new RouteData();
            routeData.Values["controller"] = "App";
            var actionContext = new ActionContext(httpContext, routeData, new ActionDescriptor());

            var model = new PlanView { Strategies = _strategies, Queue = queue };
            string html = await _renderer.RenderViewToStringAsync("Plan", model, actionContext);
            var minified = Uglify.Html(html);
            Html = minified.HasErrors ? html : minified.Code;
        }
    }
}
