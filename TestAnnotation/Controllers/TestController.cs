using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using StackExchange.Redis;

namespace TestAnnotation.Controllers;

[ApiController]
[Route("[controller]")]
public class TestController : ControllerBase
{
    private readonly Data _data;

    public TestController()
    {
        _data = new Data();
    }

    [CacheTest(Schema = "test-all")]
    [HttpGet]
    public async Task<IActionResult> GetByStatus(bool? status)
    {
        if (status == null)
        {
            var result = await _data.GetAll();
            return Ok(result);
        }

        if (status.Value)
        {
            var resultActives = await _data.GetAllActives();
            return Ok(resultActives);
        }

        var resultInactives = await _data.GetAllInactives();
        return Ok(resultInactives);
    }

    [CacheTest(Schema = "test-by-id", Expiration = 120)]
    [HttpGet("{uuid}")]
    public async Task<IActionResult> Get(Guid uuid)
    {
        var result = await _data.GetById(uuid);
        return Ok(result);
    }
}

[AttributeUsage(AttributeTargets.Method)]
public class CacheTestAttribute : ActionFilterAttribute
{
    private string _key = string.Empty;
    private ICache _cache = null!;

    public string Schema { get; set; } = string.Empty!;
    public int Expiration { get; set; } = 60;

    public override Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        _cache = context.HttpContext.RequestServices.GetService<ICache>()!;
        if (_cache.IsEnabled)
        {
            SetKey(context);

            var resultCache = _cache.Get(_key);

            if (resultCache != null)
            {
                context.Result = new OkObjectResult(JsonSerializer.Deserialize<object>(resultCache, new JsonSerializerOptions { WriteIndented = true }));
                return Task.CompletedTask;
            }
        }
        return next();
    }

    public override void OnResultExecuted(ResultExecutedContext context)
    {
        if (_cache.IsEnabled)
        {
            var result = (ObjectResult)context.Result;

            if (result != null && result.StatusCode == 200 && result.Value != null && !_cache.HasValue(_key))
            {
                _cache.Set(_key, JsonSerializer.Serialize(result.Value), Expiration);
            }
        }
        base.OnResultExecuted(context);
    }

    private void SetKey(ActionExecutingContext context)
    {
        var controller = context.ActionDescriptor.RouteValues["controller"];
        var action = context.ActionDescriptor.RouteValues["action"];
        var parameters = string.Join("-", context.ActionArguments.Select(x => $"{x.Key}-{x.Value}"));

        _key = $"{Schema}-{controller}-{action}-{parameters}";
    }
}

public interface ICache
{
    string? Get(string key);

    void Set(string key, string value, int secondsToExpire);

    bool HasValue(string key);

    bool IsEnabled { get; }
}

public class CacheRedis : ICache
{
    private readonly IDatabase? _cache;
    public bool IsEnabled { get; }

    public CacheRedis(RedisProvider cacheProvider)
    {
        _cache = cacheProvider.Cache;
        IsEnabled = _cache != null;
    }

    public string? Get(string key)
    {
        return _cache!.StringGet(key);
    }

    public bool HasValue(string key)
    {
        return _cache!.KeyExists(key);
    }

    public void Set(string key, string value, int secondsToExpire)
    {
        _cache!.StringSet(key, value, TimeSpan.FromSeconds(secondsToExpire));
    }
}

public class RedisProvider
{
    public IDatabase Cache { get; }

    public RedisProvider(IConfiguration configuration)
    {
        var cacheEnabled = configuration.GetValue<bool>("CacheEnabled");
        if (cacheEnabled)
        {
            var coonnectionString = configuration.GetConnectionString("Redis")
                 ?? throw new InvalidOperationException("Connection string 'redis' not found.");

            var redis = ConnectionMultiplexer.Connect(coonnectionString);
            Cache = redis.GetDatabase();
        }
        else
        {
            Cache = null!;
        }
    }
}

public record User(int Id, string Name, bool IsActive = true);

public class Data
{
    private readonly Dictionary<Guid, User> _users;

    public Data()
    {
        _users = new()
        {
            { Guid.Parse("5acdbd58-14da-4048-8f1f-83359eca16bd"), new User(1,"Jerson")},
            { Guid.Parse("d9cd3c26-e5d6-45b8-b3df-fe80cc67ae17"), new User(2,"Brito")},
            { Guid.Parse("e9889f44-5791-4061-aab1-fd1bd8d41cb1"), new User(3,"Tonho")},
            { Guid.Parse("c9cae7ec-5761-4873-a855-6b1edba0482c"), new User(4,"Fulano",false)},
            { Guid.Parse("e0affce2-c4d4-45df-aacc-4dd339bccb1e"), new User(5,"Cicrano",false)},
        };
    }

    public async Task<IEnumerable<User>> GetAll()
    {
        await Task.Delay(3000);
        return _users.Select(x => x.Value);
    }

    public async Task<IEnumerable<User>> GetAllActives()
    {
        await Task.Delay(3000);
        return _users.Select(x => x.Value).Where(x => x.IsActive);
    }

    public async Task<IEnumerable<User>> GetAllInactives()
    {
        await Task.Delay(3000);
        return _users.Select(x => x.Value).Where(x => !x.IsActive);
    }

    public async Task<User> GetById(Guid uuid)
    {
        await Task.Delay(3000);
        return _users[uuid];
    }
}