using NUglify;

namespace MyTimetable.Rendering
{
    public sealed record PlanPageConfig(
        IReadOnlyCollection<string> PrebuiltNames,
        IReadOnlyCollection<string> PickerNames,
        IReadOnlyCollection<string> SlotterNames
    );

    // Готовый (отрендеренный + минифицированный) HTML страницы планирования.
    public sealed class PlanPage
    {
        private readonly ViewRenderer _renderer;
        private readonly IServiceProvider _serviceProvider;
        private readonly PlanPageConfig _config;

        public string Html { get; private set; } = "";

        public PlanPage(ViewRenderer renderer, IServiceProvider serviceProvider, PlanPageConfig config)
        {
            _renderer = renderer;
            _serviceProvider = serviceProvider;
            _config = config;
        }

        public async Task Rebuild(IReadOnlyDictionary<string, int> queue)
        {
            using var scope = _serviceProvider.CreateScope();

            var model = new PlanView
            {
                PrebuiltNames = _config.PrebuiltNames,
                PickerNames = _config.PickerNames,
                SlotterNames = _config.SlotterNames,
                Queue = queue
            };
            string html = await _renderer.RenderViewToStringAsync("Plan", model, scope.ServiceProvider);
            var minified = Uglify.Html(html);
            Html = minified.HasErrors ? html : minified.Code;
        }
    }
}
