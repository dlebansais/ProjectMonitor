namespace GitHubApi
{
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;

    internal class HttpHelper
    {
        public static async Task<Stream?> Download(string url)
        {
            HttpClient Request = new HttpClient();
            HttpResponseMessage Response = await Request.GetAsync(url);
            if (Response.StatusCode != HttpStatusCode.OK)
                return null;

            HttpContent ResponseContent = Response.Content;
            return await ResponseContent.ReadAsStreamAsync();
        }
    }
}
