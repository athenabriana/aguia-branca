namespace AguiaBranca.Infrastructure.Configuration;

public sealed class CorsOptions
{
    public const string Section = "Cors";
    public string[] Origins { get; set; } = [];
}
