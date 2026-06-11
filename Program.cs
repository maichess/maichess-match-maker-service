using System.Text;
using System.Text.Json.Serialization;
using Grpc.Net.Client;
using Maichess.Engine.V1;
using Maichess.MatchManager.V1;
using MaichessMatchMakerService.Queue;
using MaichessMatchMakerService.Rest;
using MaichessMatchMakerService.Streaming;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using StackExchange.Redis;

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

// Real-time delivery and match creation always go over Kafka (socket.outbound.v1 +
// matchmaking.events.v1, match.commands.v1); the legacy Socket.EmitEvent / synchronous
// Matches.CreateMatch transports were removed in Kafka task 09. Bot-vs-bot creation still
// uses the Matches gRPC client above for synchronous start_fen validation.
builder.Services.AddSingleton<IMatchmakingNotifier, KafkaMatchmakingNotifier>();
builder.Services.AddSingleton<IMatchCreator, KafkaMatchCreator>();

// Streamiz: the single user-rating KTable + co-partitioned join, hosting skill-based
// pairing's local rating lookups (no GetUser RPC). See caching-and-read-models.md.
builder.Services.AddSingleton<MatchMakerStreams>();
builder.Services.AddSingleton<IUserRatingStore>(sp => sp.GetRequiredService<MatchMakerStreams>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<MatchMakerStreams>());

// Anti-cheat flag read model: in-memory store materialised from the compacted
// cheat.events.v1 topic; the matchmaking toggle reads it locally per pairing pass.
// See knowledge/services/anticheat-service.md.
builder.Services.AddSingleton<CheatFlagStore>();
builder.Services.AddSingleton<ICheatFlagStore>(sp => sp.GetRequiredService<CheatFlagStore>());
builder.Services.AddHostedService<CheatFlagConsumer>();

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

string otlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")
    ?? "http://otel-collector:4317";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("match-maker-service"))
    .WithTracing(t => t
        .AddAspNetCoreInstrumentation()
        .AddGrpcClientInstrumentation()
        .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)));

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
[JsonSerializable(typeof(TimeFormatResponse))]
[JsonSerializable(typeof(TimeFormatsResponse))]
[JsonSerializable(typeof(BotMatchRequest))]
[JsonSerializable(typeof(BotMatchResponse))]
internal sealed partial class AppJsonSerializerContext : JsonSerializerContext
{
}
