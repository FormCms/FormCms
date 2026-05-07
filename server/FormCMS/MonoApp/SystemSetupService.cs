using System.IO.Compression;
using FormCMS.Auth.Models;
using FormCMS.Auth.Services;
using FormCMS.Infrastructure.RelationDbDao;
using FormCMS.Utils.ResultExt;
using Microsoft.EntityFrameworkCore;
using FormCMS;

namespace FormCMS.MonoApp;

public interface ISystemSetupService
{
    Task<(bool DatabaseReady, bool HasHasSuperAdmin)> GetSystemStatus(CancellationToken ct);
    Task UpdateDatabaseConfig(DatabaseProvider databaseProvider, string connectionString);
    Task SetupSuperAdmin(SuperAdminRequest request, CancellationToken ct);
    Task AddSpa(IFormFile file, string path, string dir);
    Spa[] GetSpas();
    Task DeleteSpa(string path);
    Task UpdateSpaPath(string oldPath, string newPath);
    Task UpdateCorsOrigins(string[] origins);
    string[] GetCorsOrigins();

    string[] GetDownloadPlugins();
    Task DeleteDownloadPlugin(string fileName);
    Task AddDownloadPlugin(IFormFile file);

    string GetApiKey();
    Task UpdateApiKey(string apiKey);
}

public class SystemSetupService(
    SettingsStore settingsStore,
    MonoRunTime runTime,
    IServiceProvider serviceProvider,
    IHostApplicationLifetime lifetime,
    AuthConfig authConfig,
    SystemSettings systemSettings
) : ISystemSetupService
{
    public async Task<(bool DatabaseReady, bool HasHasSuperAdmin)> GetSystemStatus(CancellationToken ct)
    {
        var settings = settingsStore.Load();
        // Check if database is configured
        bool databaseReady = false;
        if (settings is not null&& !string.IsNullOrWhiteSpace(settings.ConnectionString))
        {
            try
            {
                ValidateDatabaseConnection(settings.DatabaseProvider, settings.ConnectionString);
                databaseReady = true;
            }
            catch
            {
                // ignored
            }
        }
        
        // Check if user exists (only if database is ready)
        bool hasSuperAdmin = false;
        if (databaseReady)
        {
            var accountService = serviceProvider.GetRequiredService<IAccountService>();
            try
            {
                hasSuperAdmin = await accountService.HasUser(ct);
            }
            catch
            {
                // ignored
            }
        }

        return (databaseReady, hasSuperAdmin);
    }

    public Task UpdateDatabaseConfig(DatabaseProvider databaseProvider, string connectionString)
    {
        // Check if database is already configured
        var settings = settingsStore.Load();
        if (!string.IsNullOrWhiteSpace(settings?.ConnectionString))
        {
            throw new ResultException("Database is already configured.");
        }

        // Create new settings
        settings =  new MonoSettings(
            DatabaseProvider: databaseProvider,
            ConnectionString: connectionString,
            ApiKey:""
        );

        // Validate database connection
        try
        {
            ValidateDatabaseConnection(databaseProvider, connectionString);
        }
        catch (Exception ex)
        {
            throw new ResultException($"Database validation failed: {ex.Message}");
        }

        settingsStore.Save(settings);
        RestartApp();
        return Task.CompletedTask;
    }



    public async Task SetupSuperAdmin(SuperAdminRequest request, CancellationToken ct)
    {
        var accountService = serviceProvider.GetRequiredService<IAccountService>();
        if (await accountService.HasUser(ct))
        {
            throw new ResultException("Super admin already exists.");
        }

        await accountService.EnsureUser(request.Email, request.Password, [Roles.Sa]);
    }

    public async Task AddSpa(IFormFile file, string path, string dir)
    {
        EnsurePermission();

        if (file == null || file.Length == 0)
            throw new ResultException("No file uploaded.");

        if (!Path.GetExtension(file.FileName).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            throw new ResultException("Only .zip files are allowed.");

        var targetDir = Path.Combine(runTime.AppRoot, dir);
        if (Directory.Exists(targetDir))
        {
            Directory.Delete(targetDir, true);
        }
        Directory.CreateDirectory(targetDir);

        var tempZipPath = Path.GetTempFileName();
        await using (var stream = new FileStream(tempZipPath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        try
        {
            await ZipFile.ExtractToDirectoryAsync(tempZipPath, targetDir);
            
            if (!File.Exists(Path.Combine(targetDir, "index.html")))
            {
                var subDirs = Directory.GetDirectories(targetDir);
                foreach (var subDir in subDirs)
                {
                    if (!File.Exists(Path.Combine(subDir, "index.html"))) continue;
                    // Move everything from subDir to targetDir
                    foreach (var subFile in Directory.GetFiles(subDir))
                    {
                        var destFile = Path.Combine(targetDir, Path.GetFileName(subFile));
                        if (File.Exists(destFile)) File.Delete(destFile);
                        File.Move(subFile, destFile);
                    }
                    foreach (var subSubDir in Directory.GetDirectories(subDir))
                    {
                        var destDir = Path.Combine(targetDir, Path.GetFileName(subSubDir));
                        if (Directory.Exists(destDir)) Directory.Delete(destDir, true);
                        Directory.Move(subSubDir, destDir);
                    }
                    Directory.Delete(subDir, true);
                    break;
                }
            }
        }
        finally
        {
            if (File.Exists(tempZipPath))
                File.Delete(tempZipPath);
        }

        var settings = settingsStore.Load();
        if (settings != null)
        {
            var spas = settings.Spas?.ToList() ?? new List<Spa>();
            spas.RemoveAll(s => s.Path == path);
            spas.Add(new Spa(path, dir));
            
            var newSettings = settings with { Spas = spas.ToArray() };
            settingsStore.Save(newSettings);
        }

        RestartApp();
    }

    public Spa[] GetSpas()
    {
        EnsurePermission();
        var settings = settingsStore.Load();
        return settings?.Spas ?? [];
    }

    public Task DeleteSpa(string path)
    {
        EnsurePermission();
        var settings = settingsStore.Load();
        if (settings?.Spas == null) return Task.CompletedTask;

        var spa = settings.Spas.FirstOrDefault(s => s.Path == path);
        if (spa == null) return Task.CompletedTask;

        var spas = settings.Spas.ToList();
        spas.Remove(spa);
        
        var newSettings = settings with { Spas = spas.ToArray() };
        settingsStore.Save(newSettings);

        var targetDir = Path.Combine(runTime.AppRoot, spa.Dir);
        if (Directory.Exists(targetDir))
        {
            Directory.Delete(targetDir, true);
        }

        RestartApp();
        return Task.CompletedTask;
    }

    public Task UpdateSpaPath(string oldPath, string newPath)
    {
        EnsurePermission();
        var settings = settingsStore.Load();
        if (settings?.Spas == null)
            throw new ResultException("No SPAs configured.");

        var spas = settings.Spas.ToList();
        var index = spas.FindIndex(s => s.Path == oldPath);
        if (index < 0)
            throw new ResultException($"SPA with path '{oldPath}' not found.");

        if (spas.Any(s => s.Path == newPath && s.Path != oldPath))
            throw new ResultException($"A SPA with path '{newPath}' already exists.");

        spas[index] = spas[index] with { Path = newPath };
        var newSettings = settings with { Spas = spas.ToArray() };
        settingsStore.Save(newSettings);

        RestartApp();
        return Task.CompletedTask;
    }

    public Task UpdateCorsOrigins(string[] origins)
    {
        EnsurePermission();
        var settings = settingsStore.Load();
        if (settings == null) throw new ResultException("Settings not found");

        var newSettings = settings with { CorsOrigins = origins };
        settingsStore.Save(newSettings);
        RestartApp();
        return Task.CompletedTask;
    }

    public string[] GetCorsOrigins()
    {
        EnsurePermission();
        var settings = settingsStore.Load();
        return settings?.CorsOrigins ?? [];
    }

    public Task UpdateApiKey(string apiKey)
    {
        EnsurePermission();
        var settings = settingsStore.Load();
        if (settings == null) throw new ResultException("Settings not found");

        var newSettings = settings with { ApiKey = apiKey ?? "" };
        settingsStore.Save(newSettings);
        authConfig.KeyAuthConfig?.Key = apiKey;
        return Task.CompletedTask;
    }

    public string GetApiKey()
    {
        EnsurePermission();
        var settings = settingsStore.Load();
        return settings?.ApiKey ?? "";
    }

    public string[] GetDownloadPlugins()
    {
        EnsurePermission();
        if (!Directory.Exists(systemSettings.DownloadPluginPath))
        {
            return [];
        }

        return Directory.GetFiles(systemSettings.DownloadPluginPath, "*.dll")
            .Select(Path.GetFileName).Where(x => x != null).ToArray()!;
    }

    public Task DeleteDownloadPlugin(string fileName)
    {
        EnsurePermission();
        var filePath = Path.Combine(systemSettings.DownloadPluginPath, fileName);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        RestartApp();
        return Task.CompletedTask;
    }

    public async Task AddDownloadPlugin(IFormFile file)
    {
        EnsurePermission();
        if (file.Length == 0)
        {
            throw new ResultException("File is empty");
        }

        if (Path.GetExtension(file.FileName).ToLower() != ".dll")
        {
            throw new ResultException("Only .dll files are allowed");
        }

        if (!Directory.Exists(systemSettings.DownloadPluginPath))
        {
            Directory.CreateDirectory(systemSettings.DownloadPluginPath);
        }

        var filePath = Path.Combine(systemSettings.DownloadPluginPath, file.FileName);
        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);
        RestartApp();
    }

    private void EnsurePermission()
    {
        var profileService = serviceProvider.GetService<IProfileService>();
        if (profileService is not null && !profileService.HasRole(Roles.Sa))
        {
            throw new ResultException("No Permission");
        }
    }

    private void ValidateDatabaseConnection(
        DatabaseProvider databaseProvider,
        string connectionString)
    {
        try
        {
            var optionsBuilder = new DbContextOptionsBuilder<CmsDbContext>();

            _ = databaseProvider switch
            {
                DatabaseProvider.Sqlite =>
                    optionsBuilder.UseSqlite(connectionString),

                DatabaseProvider.Postgres =>
                    optionsBuilder.UseNpgsql(connectionString),

                DatabaseProvider.SqlServer =>
                    optionsBuilder.UseSqlServer(connectionString),

                DatabaseProvider.Mysql =>optionsBuilder.UseMySQL(connectionString),

                _ => throw new InvalidOperationException(
                    $"Unsupported database provider: {databaseProvider}")
            };

            using var context = new CmsDbContext(optionsBuilder.Options);

            // Force a real connection attempt
            context.Database.OpenConnection();
            context.Database.CloseConnection();
        }
        catch (Exception ex)
        {
            throw new Exception(
                $"Failed to connect to database ({databaseProvider}). Reason: {ex.Message}",
                ex);
        }
    }
 

    private void RestartApp()
    {
        Task.Run(async () =>
        {
            await Task.Delay(500);
            lifetime.StopApplication();
        });
    }
}