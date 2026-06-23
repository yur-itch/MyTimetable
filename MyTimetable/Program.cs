using Microsoft.AspNetCore.ResponseCompression;
using System.IO.Compression;

using MyTimetable;
using Microsoft.AspNetCore.Mvc.Razor;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ScheduleData>();
builder.Services.AddSingleton<ViewRenderer>();
//builder.Services.AddResponseCompression(options =>
//{
//    options.EnableForHttps = true;
//    options.Providers.Add<BrotliCompressionProvider>();
//    options.Providers.Add<GzipCompressionProvider>();
//});
//builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
//{
//    options.Level = CompressionLevel.Optimal;
//});
builder.Services.AddHostedService<CacheWorker>();
var app = builder.Build();
//app.UseResponseCompression();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();
