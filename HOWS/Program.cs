using System.Net;
using System.Text;

namespace HOWS;

public class Program
{
    private static string WEB_ROOT = "";
    private const string HOST_DOMAIN = "http://192.168.1.7:3058";

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

            string uri = req?.Url?.ToString()!;
            string path = uri.Replace(HOST_DOMAIN, WEB_ROOT);
            
            if (!File.Exists(path))
            {
                if (!path.EndsWith("/"))
                {
                    resp.StatusCode = (int)HttpStatusCode.MovedPermanently;
                    resp.Headers.Add($"Location: {uri}/");
                    resp.Close();
                    continue;
                }
            }

            Console.WriteLine($"{req?.HttpMethod} {uri} ({req?.UserHostAddress})");
            Console.WriteLine($"URL: {uri}");
            Console.WriteLine($"Path: {path}");

            string? dirPath = $"{HOST_DOMAIN}/{req?.RawUrl}".Replace(HOST_DOMAIN, WEB_ROOT);
            string? filePath = "";
            bool isIndex = false;
            if (Directory.Exists(dirPath))
            {
                isIndex = true;
                filePath = Path.Combine(dirPath, "index.html");
            }
            else if (File.Exists(dirPath))
            {
                filePath = dirPath;
            }
            dirPath = Directory.GetParent(dirPath ?? "")?.FullName;
            Console.WriteLine(dirPath);
            Console.WriteLine(filePath);
            Console.WriteLine();

            byte[] data;
            if (!File.Exists(filePath))
            {
                if (isIndex)
                {
                    string[] dirs = Directory.GetDirectories(dirPath ?? "");
                    string[] files = Directory.GetFiles(dirPath ?? "");
                    StringBuilder sb = new($"<h1>Index of {req?.RawUrl}</h1><ul>");
                    foreach (string dir in dirs)
                    {
                        string d = dir.Replace(WEB_ROOT, "").Remove(0, 1);
                        sb.AppendLine($"<li><a href='{d}'>{d}/</a></li>");
                    }
                    foreach (string file in files)
                    {
                        string f = file.Replace(WEB_ROOT, "").Remove(0, 1);
                        sb.AppendLine($"<li><a href='{f}'>{f}</a></li>");
                    }
                    sb.Append("</ul>");
                    data = Encoding.UTF8.GetBytes(sb.ToString());
                    resp.ContentType = "text/html";
                    resp.ContentEncoding = Encoding.UTF8;
                    resp.ContentLength64 = data.LongLength;
                    resp.StatusCode = (int)HttpStatusCode.OK;
                }
                else
                {
                    string custom404 = Path.Combine(dirPath ?? "", "404.html");
                    if (File.Exists(custom404))
                    {
                        data = File.ReadAllBytes(custom404);
                    }
                    else
                    {
                        data = Encoding.UTF8.GetBytes(@"<html><head><title>404 Not Found</title></head><body><center><h1>404 Not Found</h1></center><hr><center>HOWS 0.0.1</center></body></html>");
                    }
                    resp.ContentType = "text/html";
                    resp.ContentEncoding = Encoding.UTF8;
                    resp.ContentLength64 = data.LongLength;
                    resp.StatusCode = (int)HttpStatusCode.NotFound;
                }
            }
            else
            {
                data = File.ReadAllBytes(filePath);
                resp.ContentType = GetContentType(filePath);
                resp.ContentEncoding = Encoding.UTF8;
                resp.ContentLength64 = data.LongLength;
                resp.StatusCode = (int)HttpStatusCode.OK;
            }

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
