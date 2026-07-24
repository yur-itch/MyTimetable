using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace MyTimetable.Rendering
{
    public sealed class ViewRenderer
    {
        private IRazorViewEngine _viewEngine;
        private ITempDataProvider _tempDataProvider;

        public ViewRenderer(IRazorViewEngine viewEngine, ITempDataProvider tempDataProvider)
        {
            _viewEngine = viewEngine;
            _tempDataProvider = tempDataProvider;
        }

        public Task<string> RenderViewToStringAsync(
            string viewName, object model,
            IServiceProvider serviceProvider,
            bool isPartial = false)
        {
            var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };
            var routeData = new RouteData();
            routeData.Values["controller"] = "App";
            var actionContext = new ActionContext(httpContext, routeData, new ActionDescriptor());
            return RenderViewToStringAsync(viewName, model, actionContext, isPartial);
        }

        public async Task<string> RenderViewToStringAsync(
            string viewName, object model,
            ActionContext actionContext,
            bool isPartial = false)
        {
            using (var sw = new StringWriter())
            {
                var viewResult = _viewEngine.FindView(actionContext, viewName, !isPartial);

                if (viewResult?.View == null)
                    throw new ArgumentException($"{viewName} does not match any available view");

                var viewDictionary =
                    new ViewDataDictionary(new EmptyModelMetadataProvider(),
                        new ModelStateDictionary())
                    { Model = model };

                var viewContext = new ViewContext(
                    actionContext,
                    viewResult.View,
                    viewDictionary,
                    new TempDataDictionary(actionContext.HttpContext, _tempDataProvider),
                    sw,
                    new HtmlHelperOptions()
                );

                await viewResult.View.RenderAsync(viewContext);
                return sw.ToString();
            }
        }
    }
}
