using System.Net.Http;

namespace GalgameManager.WinApp.Base.Contracts;

public interface IHttpClientProvider
{
    public HttpClient? HttpClient { get; }
}