using System.Net;
using System.Text;
using System.Text.Json;
using System.Diagnostics;

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

            if (!File.Exists(resPath))
            {
                if (!resPath.EndsWith("/"))
                {
                    resp.StatusCode = (int)HttpStatusCode.MovedPermanently;
                    resp.Headers.Add($"Location: {req?.Url}/");
                    resp.Close();
                    continue;
                }
            }

            byte[] data = [];
            // If the request is a directory
            if (Directory.Exists(resPath))
            {
                bool indexHandled = false;
                foreach (string ext in Config.AutoExtensions)
                {
                    string indexFile = Path.Combine(resPath, $"index.{ext}");
                    if (File.Exists(indexFile))
                    {
                        data = ReadFile(indexFile, ref resp);
                        indexHandled = true;
                        break;
                    }
                }

                // Otherwise, list all items in the directory
                if (!indexHandled)
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
                    data = ReadErrorFile();
                }
                resp.ContentType = "text/html";
                resp.ContentEncoding = Encoding.UTF8;
                resp.ContentLength64 = data.LongLength;
                resp.StatusCode = (int)HttpStatusCode.NotFound;
            }
            // The request is a file, read it and send the data
            else
            {
                data = ReadFile(resPath, ref resp);
            }
            //Console.WriteLine(dirPath);
            //Console.WriteLine($"File: {resPath}");
            Console.WriteLine();

            resp.Headers.Add("Server", $"HOWS/{(Config.ShowVersion ? Utils.GetVersion() : "?")}");
            await resp.OutputStream.WriteAsync(data, 0, data.Length);
            resp.Close();
        }
    }

    private static byte[] ReadFile(string filePath, ref HttpListenerResponse resp)
    {
        byte[] data = [];
        try
        {
            FileInfo fileInfo = new FileInfo(filePath);
            if (fileInfo.Extension == ".php")
            {
                Process process = new()
                {
                    StartInfo = new()
                    {
                        FileName = "php",
                        Arguments = @$"""{filePath}""",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    }
                };
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                int exitCode = process.ExitCode;
                if (exitCode != 0)
                {
                    data = ReadErrorFile("500 Internal Server Error", "500 Internal Server Error");
                    resp.StatusCode = (int)HttpStatusCode.InternalServerError;
                }
                else
                {
                    data = Encoding.UTF8.GetBytes(output);
                }
            }
            else
            {
                data = File.ReadAllBytes(filePath);
            }
            resp.ContentType = GetContentType(filePath);
            resp.ContentEncoding = Encoding.UTF8;
            resp.ContentLength64 = data.LongLength;
            resp.StatusCode = (int)HttpStatusCode.OK;
        }
        catch
        {
            resp.StatusCode = (int)HttpStatusCode.InternalServerError;
        }
        return data;
    }

    private static byte[] ReadErrorFile(string title = "", string message = "")
    {
        string page = File.ReadAllText("Resources/error.html");
        page = page.Replace("@title", title).Replace("@message", message);
        page = page.Replace("@version", Config.ShowVersion ? Utils.GetVersion() : "");
        return Encoding.UTF8.GetBytes(page);
    }

    private static string GetContentType(string filePath)
    {
        FileInfo info = new(filePath);
        return info.Extension switch
        {
            ".html" or ".htm" or ".php" => "text/html",
            ".css" => "text/css",
            _ => "text/plain"
        };
    }
}
