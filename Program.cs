using System.Text;
using System.Text.Json.Serialization;
using Grpc.Net.Client;
using Maichess.Engine.V1;
using Maichess.MatchManager.V1;
using MaichessMatchMakerService.Queue;
using MaichessMatchMakerService.Rest;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;

using SocketSvc = Socket.V1.Socket;

DotNetEnv.Env.Load();
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Redis
string redisUrl = builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException("ConnectionStrings:Redis is not configured");
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisUrl));
builder.Services.AddSingleton<IQueueRepository, QueueRepository>();

// gRPC client — Match Manager
string matchManagerUrl = builder.Configuration["MatchManager:Url"]
    ?? throw new InvalidOperationException("MatchManager:Url is not configured");
var matchManagerChannel = GrpcChannel.ForAddress(matchManagerUrl);
builder.Services.AddSingleton(new Matches.MatchesClient(matchManagerChannel));

// gRPC client — Engine
string engineUrl = builder.Configuration["Engine:Url"]
    ?? throw new InvalidOperationException("Engine:Url is not configured");
builder.Services.AddSingleton(new Bots.BotsClient(GrpcChannel.ForAddress(engineUrl)));

// gRPC client — Socket service
string socketServiceUrl = builder.Configuration["Services:SocketService"]
    ?? throw new InvalidOperationException("Services:SocketService is not configured");
builder.Services.AddSingleton(new SocketSvc.SocketClient(GrpcChannel.ForAddress(socketServiceUrl)));
builder.Services.AddSingleton<SocketNotifier>();

// Queue service and background matching worker
builder.Services.AddSingleton<QueueingService>();
builder.Services.AddSingleton<MatchingService>();
builder.Services.AddHostedService<MatchingWorker>();

// JWT auth
string jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is not configured");
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (context.Request.Cookies.TryGetValue("access_token", out string? token))
                {
                    context.Token = token;
                }

                return Task.CompletedTask;
            },
        };
    });

builder.Services.AddAuthorization();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower;
});

builder.Services.AddOpenApi();

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapQueueEndpoints();

app.Run();

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(QueueRequest))]
[JsonSerializable(typeof(QueueResponse))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(BotResponse))]
[JsonSerializable(typeof(BotsListResponse))]
internal sealed partial class AppJsonSerializerContext : JsonSerializerContext
{
}
