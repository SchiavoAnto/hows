using System.Net;
using System.Text;
using System.Reflection;

namespace HOWS;

public class Program
{
    private static string WEB_ROOT = "";
    private const string HOST_DOMAIN = "http://localhost:3058";

    private static HttpListener? listener;

    public static void Main(string[] args)
    {
        try
        {
            if (!Directory.Exists("www"))
            {
                Directory.CreateDirectory("www");
            }
            WEB_ROOT = $"{AppDomain.CurrentDomain.BaseDirectory}www";

            listener = new HttpListener();
            listener.Prefixes.Add($"{HOST_DOMAIN}/");
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
            string resPath = $"{WEB_ROOT}{absolutePath}";

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
                    string[] dirs = Directory.GetDirectories(resPath);
                    string[] files = Directory.GetFiles(resPath);
                    string baseFile = File.ReadAllText("Resources/dirlist.html");
                    StringBuilder content = new($"<h1>Index of {absolutePath}</h1><ul>");
                    foreach (string dir in dirs)
                    {
                        string d = dir.Replace(WEB_ROOT, "").Remove(0, 1);
                        content.AppendLine($"<li><a href='{d}'>{d}/</a></li>");
                    }
                    foreach (string file in files)
                    {
                        string f = file.Replace(WEB_ROOT, "").Remove(0, 1);
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
                    page = page.Replace("@version", Assembly.GetExecutingAssembly().GetName().Version?.ToString());
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
