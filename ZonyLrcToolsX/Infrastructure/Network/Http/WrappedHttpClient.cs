using Newtonsoft.Json;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using ZonyLrcToolsX.Infrastructure.Configuration;
using ZonyLrcToolsX.Infrastructure.Network.Http.Exceptions;
using ZonyLrcToolsX.Infrastructure.Utils;

// ReSharper disable NotResolvedInText

namespace ZonyLrcToolsX.Infrastructure.Network.Http
{
    public class WrappedHttpClient : IDisposable
    {
        private readonly HttpClient _httpClient;

        public WrappedHttpClient()
        {
            // 如果启用了网络代理，则构建时增加网络代理参数。
            if (AppConfiguration.Instance.Configuration.IsEnableProxy &&
                !AppConfiguration.Instance.Configuration.ProxyIp.IsNullOrEmptyOrWhite())
            {
                _httpClient = new HttpClient(new HttpClientHandler
                {
                    Proxy = new WebProxy($"{AppConfiguration.Instance.Configuration.ProxyIp}:{AppConfiguration.Instance.Configuration.ProxyPort}"),
                    UseProxy = true
                });
            }
            else
            {
                _httpClient = new HttpClient();
            }
        }

        /// <summary>
        /// 针对目标 Url 发起 Http Get 请求，并返回请求的结果。
        /// </summary>
        /// <param name="url">需要请求的目标 Url 地址。</param>
        /// <param name="parameters">执行 Http Get 请求时携带的参数。</param>
        /// <param name="refererUrl">执行请求时的来源地址。</param>
        /// <returns>目标服务器的响应结果。</returns>
        public async Task<string> GetAsync(string url,object parameters = null,string refererUrl = null)
        {
            var requestParamsStr = BuildFormParameters(parameters);
            var requestMsg = new HttpRequestMessage(HttpMethod.Get,new Uri($"{url}?{requestParamsStr}"));

            // ReSharper disable once AssignNullToNotNullAttribute
            if(!refererUrl.IsNullOrEmptyOrWhite()) requestMsg.Headers.Referrer = new Uri(refererUrl);

            using (var responseMsg = await _httpClient.SendAsync(requestMsg))
            {
                var responseContent = await responseMsg.Content.ReadAsStringAsync();
                if(responseMsg.StatusCode != HttpStatusCode.OK) throw new HttpRequestFailedException("对目标服务器执行 Http Get 请求失败。", requestParamsStr, responseContent);
                return responseContent;
            }
        }

        public async Task<string> PostAsync(string url,object parameters,bool isBuildForm = false,string refererUrl = null, string mediaTypeValue = null)
        {
            var requestParamsStr = isBuildForm ? BuildFormParameters(parameters) : BuildJsonParameters(parameters);
            var requestMsg = new HttpRequestMessage(HttpMethod.Post, new Uri(url));
            // ReSharper disable once AssignNullToNotNullAttribute
            if (!refererUrl.IsNullOrEmptyOrWhite()) requestMsg.Headers.Referrer = new Uri(refererUrl);

            if (mediaTypeValue.IsNullOrEmptyOrWhite()) mediaTypeValue = "application/json";
            requestMsg.Content = new StringContent(requestParamsStr);
            requestMsg.Content.Headers.ContentType = new MediaTypeHeaderValue(mediaTypeValue);

            using (var responseMsg = await _httpClient.SendAsync(requestMsg))
            {
                var responseContent = await responseMsg.Content.ReadAsStringAsync();
                if (responseMsg.StatusCode != HttpStatusCode.OK) throw new HttpRequestFailedException("对目标服务器执行 Http Get 请求失败。", requestParamsStr, responseContent);
                return responseContent;
            }
        }

        public async Task<TResponse> GetAsync<TResponse>(string url,object parameters = null,string refererUrl = null)
        {
            var responseStr = await GetAsync(url, parameters, refererUrl);
            try
            {
                var responseObj = JsonConvert.DeserializeObject<TResponse>(responseStr);
                if (responseObj == null)
                {
                    throw new HttpRequestFailedException("请求结果转换为 JSON 失败。", BuildFormParameters(parameters), responseStr);
                }

                return responseObj;
            }
            catch (Exception)
            {
                throw new HttpRequestFailedException("请求结果转换为 JSON 失败。",BuildFormParameters(parameters),responseStr);
            }
        }

        public async Task<TResponse> PostAsync<TResponse>(string url, object parameters, bool isBuildForm = false, string refererUrl = null, string mediaTypeValue = null)
        {
            var responseStr = await PostAsync(url, parameters,isBuildForm, refererUrl, mediaTypeValue);
            try
            {
                var responseObj = JsonConvert.DeserializeObject<TResponse>(responseStr);
                if (responseObj == null)
                {
                    throw new HttpRequestFailedException("请求结果转换为 JSON 失败。", BuildFormParameters(parameters), responseStr);
                }

                return responseObj;
            }
            catch (Exception)
            {
                throw new HttpRequestFailedException("请求结果转换为 JSON 失败。",BuildFormParameters(parameters),responseStr);
            }
        }

        private string BuildJsonParameters(object parameters)
        {
            if (parameters == null) return string.Empty;
            if (parameters is string result) return result;

            return JsonConvert.SerializeObject(parameters);
        }

        private string BuildFormParameters(object parameters)
        {
            if(parameters == null) return string.Empty;
            var type = parameters.GetType();
            if (type == typeof(string)) return parameters as string;

            var properties = type.GetProperties();
            var paramBuilder = new StringBuilder();

            foreach (var propertyInfo in properties)
            {
                paramBuilder.Append($"{propertyInfo.Name}={propertyInfo.GetValue(parameters)}&");
            }

            return paramBuilder.ToString().TrimEnd('&');
        }

        private string UrlEncodingByUtf8(string srcText)
        {
            return HttpUtility.UrlEncode(srcText, Encoding.UTF8);
        }

        private string UrlEncodingByUtf8(object srcText)
        {
            if(srcText is string newText)
            {
                return HttpUtility.UrlEncode(newText, Encoding.UTF8);

            }

            return string.Empty;
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}