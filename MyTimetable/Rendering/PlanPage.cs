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

            var model = new PlanView
            {
                PrebuiltNames = _prebuiltNames,
                PickerNames = _pickerNames,
                SlotterNames = _slotterNames,
                Queue = queue
            };
            string html = await _renderer.RenderViewToStringAsync("Plan", model, scope.ServiceProvider);
            var minified = Uglify.Html(html);
            Html = minified.HasErrors ? html : minified.Code;
        }
    }
}
