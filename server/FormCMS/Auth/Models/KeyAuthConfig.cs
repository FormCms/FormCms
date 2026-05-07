namespace FormCMS.Auth.Models;

public class KeyAuthConfig(string key)
{
    public string Key { get; set; } = key;
}