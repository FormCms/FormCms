using System.Text.Json;
using System.Text.Json.Serialization;
using FormCMS;
using FormCMS.Auth.Models;
using FormCMS.Cms.Services;
using FormCMS.Core.Descriptors;
using FormCMS.MonoApp;
using System.IO; // Added for Path.Combine and File.ReadAllText
using System;
using System.Security.Claims;
using FormCMS.Auth.Services;
using FormCMS.Core.Identities; // Added for AppContext.BaseDirectory

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var dataPath = builder.Configuration.GetValue<string>("FORMCMS_DATA_PATH")??"";
var sizeStr = builder.Configuration.GetValue<string>("FORMCMS_MAX_REQUEST_SIZE");
builder.SetMaxRequestBody(sizeStr);

var settings = builder.AddMonoApp(dataPath);
var app = builder.Build();
app.MapConfigEndpoints();
app.UseMonoCors();
app.MapReverseProxy();
app.MapCorsEndpoints();

if (!string.IsNullOrWhiteSpace(settings?.ConnectionString)   && await app.EnsureDbCreatedAsync())
{
    await app.UseCmsAsync();
    app.MapSpas();
    
    await using var scope = app.Services.CreateAsyncScope();
    var entitiesFilePath = Path.Combine(AppContext.BaseDirectory, "entities.json");
    if (File.Exists(entitiesFilePath))
    {
        var httpContextAccessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();

        // Create a fake admin/system principal
        var identity = new ClaimsIdentity("Seeding");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "system-seed"));
        identity.AddClaim(new Claim(ClaimTypes.Name, "SystemSeeder"));
        identity.AddClaim(new Claim(ClaimTypes.Role, Roles.Sa));   // or whatever role has full permissions

        httpContextAccessor.HttpContext ??= new DefaultHttpContext(); // in case it's null
        httpContextAccessor.HttpContext.User = new ClaimsPrincipal(identity);
        
        var entitySchemaService = scope.ServiceProvider.GetRequiredService<IEntitySchemaService>();
        var jsonContent = await File.ReadAllTextAsync(entitiesFilePath);
        var jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
        var schemas = JsonSerializer.Deserialize<Schema[]>(jsonContent, jsonSerializerOptions) ?? Array.Empty<Schema>();

        foreach (var schema in schemas)
        {
            var existing =
                await entitySchemaService.LoadEntity(schema.Name, PublicationStatus.Published, CancellationToken.None);
            if (!existing.IsSuccess)
            {
                await entitySchemaService.SaveTableDefine(schema, true, CancellationToken.None);
            }
        }
        
        var accountService = scope.ServiceProvider.GetRequiredService<IAccountService>();
        await accountService.EnsureRoleAccess(new RoleAccess(
            Name:Roles.User,
            RestrictedReadonlyEntities:[],
            RestrictedReadWriteEntities:schemas.Select(x=>x.Name).ToArray(),
            ReadWriteEntities:[],
            ReadonlyEntities:[]
            ));
    }

}


app.Run();
