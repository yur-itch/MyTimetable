using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using MyTimetable.Planning;
using NUglify;

namespace MyTimetable.Rendering
{
    // Готовый (отрендеренный + минифицированный) HTML страницы планирования.
    public sealed class PlanPage
    {
        private readonly ViewRenderer _renderer;
        private readonly IServiceProvider _serviceProvider;
        private readonly IReadOnlyCollection<string> _prebuiltNames;
        private readonly IReadOnlyCollection<string> _pickerNames;
        private readonly IReadOnlyCollection<string> _slotterNames;

        public string Html { get; private set; } = "";

        public PlanPage(ViewRenderer renderer, IServiceProvider serviceProvider,
                        IReadOnlyDictionary<string, IPlanningSelectorFactory> strategies,
                        IReadOnlyDictionary<string, IPickerFactory> pickers,
                        IReadOnlyDictionary<string, ISlotterFactory> slotters)
        {
            _renderer = renderer;
            _serviceProvider = serviceProvider;
            _prebuiltNames = strategies.Keys.ToList();
            _pickerNames = pickers.Keys.ToList();
            _slotterNames = slotters.Keys.ToList();
        }

        public async Task Rebuild(IReadOnlyDictionary<string, int> queue)
        {
            using var scope = _serviceProvider.CreateScope();
            var httpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
            var routeData = new RouteData();
            routeData.Values["controller"] = "App";
            var actionContext = new ActionContext(httpContext, routeData, new ActionDescriptor());

            var model = new PlanView
            {
                PrebuiltNames = _prebuiltNames,
                PickerNames = _pickerNames,
                SlotterNames = _slotterNames,
                Queue = queue
            };
            string html = await _renderer.RenderViewToStringAsync("Plan", model, actionContext);
            var minified = Uglify.Html(html);
            Html = minified.HasErrors ? html : minified.Code;
        }
    }
}
