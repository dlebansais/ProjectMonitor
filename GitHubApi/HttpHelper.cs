namespace GitHubApi
{
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;

    /// <summary>
    /// Tool to download files from the Internet.
    /// </summary>
    internal class HttpHelper
    {
        /// <summary>
        /// Downloads a file.
        /// </summary>
        /// <param name="url">The file URL.</param>
        /// <returns>A stream with the file content; null if an error occurred.</returns>
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
