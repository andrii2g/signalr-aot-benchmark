using Chat.Shared;
using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Services.Configure<JsonHubProtocolOptions>(options =>
{
    options.PayloadSerializerOptions.TypeInfoResolverChain.Insert(0, ChatJsonContext.Default);
});

var app = builder.Build();

app.MapGet("/", () => Results.Content(IndexHtml.Content, "text/html"));
app.MapGet("/ready", () => Results.Text("ready"));
app.MapHub<ChatHub>("/chat");

app.Run();
