namespace ASM.WebPortal.Configuration;

public class PortalHostOptions
{
    public bool UseHttpsRedirection { get; set; } = true;
    public bool ApplyMigrationsOnStartup { get; set; } = true;
    public bool SeedSystemData { get; set; } = true;
    public bool SeedDemoData { get; set; } = true;
}
