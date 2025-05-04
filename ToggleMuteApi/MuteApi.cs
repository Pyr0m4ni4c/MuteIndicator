using Microsoft.AspNetCore.Mvc;

namespace WebApplication1;

public class MuteApi
{
    private static WebApplication _app = null!;
    private static WebApplicationBuilder _builder = null!;
    public delegate void EventHandler<T>(T e);
    public delegate bool EventHandler2();
    public static event EventHandler<bool>? OnSetMuteReceived;
    public static event EventHandler2 OnToggleMuteReceived;
    public delegate void OnCycleDevicesReceivedDel();
    public static OnCycleDevicesReceivedDel OnCycleDevicesReceived;

    public static void Stop()
    {
        _app?.StopAsync();
    }

    static void InitBuilder(string url)
    {
        _builder = WebApplication.CreateBuilder();

        // Load appsettings.json
        _builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

        if (string.IsNullOrEmpty(url)) _builder.WebHost.UseUrls(new string[] { url });

        _builder.Services.AddAuthorization();

        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        _builder.Services.AddOpenApi();
    }

    private static void InitApp()
    {
        _app = _builder.Build();

        // Configure the HTTP request pipeline.
        if (_app.Environment.IsDevelopment()) { _app.MapOpenApi(); }

        //_app.UseHttpsRedirection();

        _app.UseAuthorization();
    }

    private static void AddEndpoints()
    {
        AddSetMute();
        AddToggleMute();
        AddCycleDevices();
        return;

        void AddCycleDevices()
        {
            _app.MapGet("/CycleDevices", () =>
                {
                    OnCycleDevicesReceived?.Invoke();
                    return Results.Ok();
                }) // Produces HTTP 200 with no content
                .WithName("CycleDevicesEP");
        }

        void AddToggleMute()
        {
            _app.MapGet("/ToggleMute", () =>
                {
                    var newState = OnToggleMuteReceived?.Invoke() ?? false;
                    return Results.Ok(new { Success = true, IsMuted = newState });
                })
                .WithName("ToggleMuteEP");
        }

        void AddSetMute()
        {
            _app.MapPost("/SetMute", async (HttpContext context, [FromBody] bool inputValue) =>
                {
                    if (OnSetMuteReceived == null) return Results.Ok(new { Success = true, IsMuted = inputValue });

                    OnSetMuteReceived.Invoke(inputValue);
                    return Results.Ok(new { Success = true, IsMuted = inputValue });

                    // async shit
                    /*// Start the asynchronous invocation of the event
                    var asyncResult = OnMuteReceived.BeginInvoke(null, inputValue, null, null);

                    // Use a separate task to handle EndInvoke properly
                    await Task.Run(() =>
                    {
                        // Wait for the async operation to complete
                        OnMuteReceived.EndInvoke(asyncResult);
                    });

                    // Return the same value as the response
                    return Results.Ok(new { Success = true, IsMuted = inputValue });*/
                })
                .WithName("SetMuteEP");
        }
    }

    public void Init(string url)
    {
        Task.Run(() =>
        {
            InitBuilder("http://localhost:5288");
            InitApp();
            AddEndpoints();
            _app.Run(); // Runs the web app on a background thread
        });
    }

    public static void Main(string[] args)
    {
        InitBuilder(string.Empty);
        InitApp();
        AddEndpoints();
        _app.Run();
    }
}