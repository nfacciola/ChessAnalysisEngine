using ChessAnalysis.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Domain Services
builder.Services.AddScoped<BoardAnalysisService>();

// Engine Management
builder.Services.AddSingleton<EngineManager>();
builder.Services.AddHostedService<SessionCleanupService>();

// HTTP Clients (Registers GeminiCoachService automatically)
builder.Services.AddHttpClient<GeminiCoachService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendDev", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

// Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors("FrontendDev");
}

app.UseHttpsRedirection();
app.MapControllers();
app.Run();