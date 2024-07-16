namespace HOWS;

public class Config
{
    public string HostAddress { get; set; } = "localhost";
    public string HostPort { get; set; } = "80";
    public string WebRootName { get; set; } = "www";
    public bool AllowDirList { get; set; } = true;
    public bool ShowVersion { get; set; } = true;

    public override string ToString()
    {
        return $"Config {{ HostAddress={HostAddress}, HostPort={HostPort}, WebRootName={WebRootName}, AllowDirList={AllowDirList}, ShowVersion={ShowVersion} }}";
    }
}
