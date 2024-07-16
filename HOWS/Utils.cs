using System.Reflection;

namespace HOWS;

public static class Utils
{
    public static string GetVersion()
    {
        return Assembly.GetExecutingAssembly().GetName().Version?.ToString()!;
    }
}
