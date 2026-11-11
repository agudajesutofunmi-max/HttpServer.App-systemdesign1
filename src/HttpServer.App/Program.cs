using HttpServer.App;


var server = new HttpServerImplementation(port: 8080);

try
{
    await server.StartAsync();
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Server error: {ex.Message}");
    Environment.Exit(1);
}
