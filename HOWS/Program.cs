using System.Net;
using System.Text;
using System.Text.Json;

namespace HOWS;

public class Program
{
    private static string WebRoot = "";
    private static string HostAddress = "";
    public static Config Config = new();

    private static HttpListener? listener;

    public static void Main(string[] args)
    {
        try
        {
            if (File.Exists($"{AppDomain.CurrentDomain.BaseDirectory}config.json"))
            {
                Config = JsonSerializer.Deserialize<Config>(File.ReadAllText($"{AppDomain.CurrentDomain.BaseDirectory}config.json"), new JsonSerializerOptions
                {
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                })!;
            }
            else
            {
                string configFile = JsonSerializer.Serialize(Config, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText($"{AppDomain.CurrentDomain.BaseDirectory}config.json", configFile);
            }

            WebRoot = $"{AppDomain.CurrentDomain.BaseDirectory}{Config.WebRootName}";
            if (!Directory.Exists(WebRoot))
            {
                Directory.CreateDirectory(WebRoot);
            }

            HostAddress = $"http://{Config.HostAddress}:{Config.HostPort}";

            listener = new HttpListener();
            listener.Prefixes.Add($"{HostAddress}/");
            listener.Start();

            Task responseTask = RequestCallback();
            responseTask.GetAwaiter().GetResult();

            listener.Close();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"{ex.GetType()}: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
        }
    }

    private static async Task RequestCallback()
    {
        while (true)
        {
            HttpListenerContext? ctx = await listener?.GetContextAsync()!;

            HttpListenerRequest req = ctx.Request;
            HttpListenerResponse resp = ctx.Response;

            Console.WriteLine($"{req?.HttpMethod} {req?.Url?.ToString()} ({req?.RemoteEndPoint})");
            //Console.WriteLine($"Original String: {req?.Url?.OriginalString}");
            //Console.WriteLine($"Absolute Path: {req?.Url?.AbsolutePath}");

            string absolutePath = req?.Url?.AbsolutePath ?? "";
            string resPath = $"{WebRoot}{absolutePath}";

            byte[] data;
            // If the request is a directory
            if (Directory.Exists(resPath))
            {
                string indexFile = Path.Combine(resPath, "index.html");
                // If an index page exists, get that
                if (File.Exists(indexFile))
                {
                    data = File.ReadAllBytes(indexFile);
                    resp.ContentType = GetContentType(indexFile);
                    resp.ContentEncoding = Encoding.UTF8;
                    resp.ContentLength64 = data.LongLength;
                    resp.StatusCode = (int)HttpStatusCode.OK;
                }
                // Otherwise, list all items in the directory
                else
                {
                    // If allowed to list directory contents, list them
                    if (Config.AllowDirList)
                    {
                        string[] dirs = Directory.GetDirectories(resPath);
                        string[] files = Directory.GetFiles(resPath);
                        string baseFile = File.ReadAllText("Resources/dirlist.html");
                        StringBuilder content = new($"<h1>Index of {absolutePath}</h1>");
                        content.AppendLine($"<p>{dirs.Length + files.Length} elements</p><ul>");
                        foreach (string dir in dirs)
                        {
                            string d = dir.Replace(WebRoot, "").Remove(0, 1);
                            content.AppendLine($"<li><a href='{d}'>{d}/</a></li>");
                        }
                        foreach (string file in files)
                        {
                            string f = file.Replace(WebRoot, "").Remove(0, 1);
                            content.AppendLine($"<li><a href='{f}'>{f}</a></li>");
                        }
                        content.Append("</ul>");
                        baseFile = baseFile.Replace("@title", $"Index of {absolutePath}");
                        baseFile = baseFile.Replace("@content", content.ToString());
                        data = Encoding.UTF8.GetBytes(baseFile);
                        resp.ContentType = "text/html";
                        resp.ContentEncoding = Encoding.UTF8;
                        resp.ContentLength64 = data.LongLength;
                        resp.StatusCode = (int)HttpStatusCode.OK;
                    }
                    // Otherwise, 403 forbidden
                    else
                    {
                        data = [];
                        resp.StatusCode = (int)HttpStatusCode.Forbidden;
                    }
                }
            }
            // The request is not a file, so 404
            else if (!File.Exists(resPath))
            {
                FileInfo fileInfo = new FileInfo(resPath);
                string custom404 = Path.Combine(fileInfo.Directory?.FullName ?? "", "404.html");
                if (File.Exists(custom404))
                {
                    data = File.ReadAllBytes(custom404);
                }
                else
                {
                    string page = File.ReadAllText("Resources/404.html");
                    page = page.Replace("@version", Config.ShowVersion ? Utils.GetVersion() : "");
                    data = Encoding.UTF8.GetBytes(page);
                }
                resp.ContentType = "text/html";
                resp.ContentEncoding = Encoding.UTF8;
                resp.ContentLength64 = data.LongLength;
                resp.StatusCode = (int)HttpStatusCode.NotFound;
            }
            // The request is a file, read it and send the data
            else
            {
                data = File.ReadAllBytes(resPath);
                resp.ContentType = GetContentType(resPath);
                resp.ContentEncoding = Encoding.UTF8;
                resp.ContentLength64 = data.LongLength;
                resp.StatusCode = (int)HttpStatusCode.OK;
            }
            //Console.WriteLine(dirPath);
            //Console.WriteLine($"File: {resPath}");
            Console.WriteLine();

            resp.Headers.Add("Server", $"HOWS/{(Config.ShowVersion ? Utils.GetVersion() : "?")}");
            await resp.OutputStream.WriteAsync(data, 0, data.Length);
            resp.Close();
        }
    }

    private static string GetContentType(string filePath)
    {
        FileInfo info = new(filePath);
        return info.Extension switch
        {
            ".html" or ".htm" => "text/html",
            ".css" => "text/css",
            _ => "text/plain"
        };
    }
}
