using FluentValidation;
using TestJob.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.WriteIndented = true;
        o.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "TestJob API", Version = "v1" });
});
builder.Services.AddValidatorsFromAssemblyContaining<ProcessRequestValidator>();
builder.Services.AddScoped<IProcessingService, ProcessingService>();
builder.Services.AddHostedService<DbInitHostedService>();

var app = builder.Build();

app.UseSwagger(c => { c.RouteTemplate = "api/swagger/{documentName}/swagger.json"; });
app.UseSwaggerUI(c =>
{
    c.RoutePrefix = "api/swagger";
    c.SwaggerEndpoint("/api/swagger/v1/swagger.json", "TestJob API v1");
});

app.MapControllers();

app.Run();
