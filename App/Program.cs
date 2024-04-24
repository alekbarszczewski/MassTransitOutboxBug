using App;
using App.Posts;
using App.Users;

CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

// use "docker-compose up" to run PG and RabbitMQ on required ports
var pgConnectionString = "Host=localhost;Database=test;Password=test;User ID=test;Port=5501";
var rabbitMqUrl = "rabbitmq://guest:guest@localhost:5701";

var hosts = new[]
{
    // Build POSTS WebApplication with PostsDbContext
    ApplicationFactory.Build<PostsDbContext>(
        pgConnectionString: pgConnectionString,
        rabbitMqUrl: rabbitMqUrl,
        listenUrl: "http://localhost:5000",
        serviceName: "POSTS"),
    
    // Build USERS WebApplication with UsersDbContext
    ApplicationFactory.Build<UsersDbContext>(
        pgConnectionString: pgConnectionString,
        rabbitMqUrl: rabbitMqUrl,
        listenUrl: "http://localhost:5001",
        serviceName: "USERS"),
};

// Run two web applications in parallel
var hostEntryPoints = hosts.Select<IHost, Func<Task>>(x => async () =>
{
    try
    {
        await x.RunAsync(cancellationTokenSource.Token);
    }
    catch (Exception e)
    {
        cancellationTokenSource.Cancel();
        throw;
    }
});

await Task.WhenAll(hostEntryPoints.Select(hostEntryPoint => hostEntryPoint()));