﻿// DragonFruit.Common Copyright 2020 DragonFruit Network
// Licensed under the MIT License. Please refer to the LICENSE file at the root of this project for details

using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using DragonFruit.Common.Data.Exceptions;
using DragonFruit.Common.Data.Helpers;
using DragonFruit.Common.Data.Parameters;
using DragonFruit.Common.Data.Serializers;

namespace DragonFruit.Common.Data
{
    /// <summary>
    /// <see cref="HttpClient"/>-related data
    /// </summary>
    public class ApiClient
    {
        #region Constructors

        public ApiClient()
        {
            Serializer = new ApiJsonSerializer(CultureInfo.InvariantCulture);
        }

        public ApiClient(CultureInfo culture)
        {
            Serializer = new ApiJsonSerializer(culture);
        }

        #endregion

        #region Properties

        /// <summary>
        /// The User-Agent header value
        /// </summary>
        public string UserAgent { get; set; }

        /// <summary>
        /// Additional headers to be sent with the requests
        /// </summary>
        public HashableDictionary<string, string> CustomHeaders { get; set; } = new HashableDictionary<string, string>();

        /// <summary>
        /// The Authorization value
        /// </summary>
        public string Authorization { get; set; } = null;

        /// <summary>
        /// Optional <see cref="HttpClient"/> settings sent by the <see cref="HttpClientHandler"/>
        /// </summary>
        protected HttpClientHandler Handler { get; set; } = null;

        /// <summary>
        /// Method for getting data
        /// </summary>
        protected ISerializer Serializer { get; set; }

        /// <summary>
        /// Last <see cref="ApiRequest"/> made for using with 
        /// </summary>
        private ApiRequest CachedRequest { get; set; }

        /// <summary>
        /// <see cref="HttpClient"/> used by these requests. This is used by the library and as such, should **not** be disposed in any way
        /// </summary>
        protected HttpClient Client { get; private set; }

        /// <summary>
        /// Time, in milliseconds to wait to modify a <see cref="HttpClient"/> before failing the request
        /// </summary>
        protected int AdjustmentTimeout { get; set; } = 200;

        #endregion

        #region Clients, Hashes and Locks

        private bool _clientAdjustmentInProgress;
        private string _lastClientHash = string.Empty;

        //private readonly object _clientAdjustmentLock = new object();
        private long _currentRequests = 0;

        /// <summary>
        /// Checksum that determines whether we replace the <see cref="HttpClient"/>
        /// </summary>
        protected virtual string ClientHash => $"{UserAgent.ItemHashCode()}.{CustomHeaders.ItemHashCode()}.{Handler.ItemHashCode()}.{Authorization.ItemHashCode()}";

        #endregion

        #region Default Creations

        /// <summary>
        /// Checks the current <see cref="HttpClient"/> and replaces it if headers or <see cref="Handler"/> has been modified
        /// </summary>
        protected virtual HttpClient GetClient(ApiRequest requestData)
        {
            while (_clientAdjustmentInProgress)
                Thread.Sleep(AdjustmentTimeout / 2);

            //if there's no edits return the current client
            if (_lastClientHash == ClientHash)
                return Client;

            try
            {
                _clientAdjustmentInProgress = true;

                //lock for modification
                //if (!Monitor.TryEnter(_clientAdjustmentLock, AdjustmentTimeout))
                //{
                //    throw new TimeoutException(
                //        $"The {nameof(ApiClient)} is being overloaded with reconstruction requests. Consider creating a separate {nameof(ApiClient)} and delegating clients to specific types of requests");
                //}

                //wait for all ongoing requests to end
                while (_currentRequests > 0)
                    Thread.Sleep(AdjustmentTimeout / 2);

                //cleanup current client
                Client?.Dispose();
                Client = Handler != null ? new HttpClient(Handler, true) : new HttpClient();
                var hasAuthData = !string.IsNullOrEmpty(Authorization);

                if (requestData.RequireAuth && !hasAuthData)
                    throw new ClientValidationException("Authorization data expected, but not found");

                if (hasAuthData)
                    Client.DefaultRequestHeaders.Add("Authorization", Authorization);

                if (!string.IsNullOrEmpty(UserAgent))
                    Client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);

                if (!string.IsNullOrEmpty(requestData.AcceptedContent))
                    Client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(requestData.AcceptedContent));

                foreach (var header in CustomHeaders)
                    Client.DefaultRequestHeaders.Add(header.Key, header.Value);

                SetupClient(Client);

                _lastClientHash = ClientHash;
                return Client;
            }
            finally
            {
                _clientAdjustmentInProgress = false;
                //Monitor.Exit(_clientAdjustmentLock);
            }
        }

        /// <summary>
        /// Creates the default <see cref="HttpResponseMessage"/>, which can then be overriden by <see cref="SetupRequest"/>
        /// </summary>
        private HttpRequestMessage GetRequest(ApiRequest requestData)
        {
            var request = new HttpRequestMessage { RequestUri = new Uri(requestData.FullUrl) };

            //generic setup
            switch (requestData.Method)
            {
                case Methods.Get:
                    request.Method = HttpMethod.Get;
                    break;

                case Methods.Post:
                    request.Method = HttpMethod.Post;
                    request.Content = GetContent(requestData);
                    break;

                //todo putfile???
                case Methods.Put:
                    request.Method = HttpMethod.Put;
                    request.Content = GetContent(requestData);
                    break;

                case Methods.Patch:
                    request.Method = new HttpMethod("PATCH"); //in .NET standard 2 patch isn't implemented...
                    request.Content = GetContent(requestData);
                    break;

                case Methods.Delete:
                    request.Method = HttpMethod.Delete;
                    request.Content = GetContent(requestData);
                    break;

                case Methods.Head:
                    request.Method = HttpMethod.Head;
                    break;

                default:
                    throw new NotImplementedException();
            }

            return request;
        }

        private HttpContent GetContent(ApiRequest requestData)
        {
            switch (requestData.DataType)
            {
                case DataTypes.Encoded:
                    return new FormUrlEncodedContent(requestData.GetParameter<FormParameter>());

                case DataTypes.Serialized:
                    return Serializer.Serialize(requestData);

                case DataTypes.SerializedProperty:
                    var body = Serializer.Serialize(requestData.GetSingleParameterObject<RequestBody>());
                    return body;

                case DataTypes.Custom:
                    return requestData.BodyContent;

                default:
                    //todo custom exception - there should have been a datatype specified
                    throw new ArgumentOutOfRangeException();
            }
        }

        #endregion

        #region Empty Overrides (Inherited)

        /// <summary>
        /// Add your own headers/settings to the <see cref="HttpClient"/> being created. Runs after the headers have been added
        /// </summary>
        /// <param name="client"></param>
        protected virtual void SetupClient(HttpClient client)
        {
        }

        /// <summary>
        /// When overridden, this can be used to alter the <see cref="HttpRequestMessage"/> post-creation
        /// </summary>
        protected virtual void SetupRequest(HttpRequestMessage request)
        {
        }

        #endregion

        /// <summary>
        /// Perform an <see cref="ApiRequest"/> with a specified return type.
        /// </summary>
        public virtual T Perform<T>(ApiRequest requestData) where T : class
        {
            if (string.IsNullOrWhiteSpace(requestData.Path))
                throw new NullRequestException();

            //cache in case we need to PerformLast<T>();
            CachedRequest = requestData;

            //get client and request (disposables)
            var client = GetClient(requestData);
            Interlocked.Increment(ref _currentRequests);

            var request = GetRequest(requestData);

            //post-modification
            SetupRequest(request);

            //send request
            var response = client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            //un-bump reqs
            Interlocked.Decrement(ref _currentRequests);

            //validate and process
            var output = ValidateAndProcess<T>(response);

            //dispose
            response.Result.Content.Dispose();
            response.Result.Dispose();
            response.Dispose();

            request.Dispose();

            //return
            return output;
        }

        /// <summary>
        /// Perform a <see cref="ApiRequest"/> that returns the response message. The <see cref="HttpResponseMessage"/> returned cannot be used for reading data, as the underlying <see cref="Task"/> will be disposed.
        /// </summary>
        /// <param name="requestData"></param>
        public virtual HttpResponseMessage Perform(ApiRequest requestData)
        {
            if (string.IsNullOrWhiteSpace(requestData.Path))
                throw new NullRequestException();

            //cache in case we need to PerformLast<T>();
            CachedRequest = requestData;

            //get client and request (disposables)
            var client = GetClient(requestData);
            Interlocked.Increment(ref _currentRequests);

            var request = GetRequest(requestData);

            //post-modification
            SetupRequest(request);

            //send request
            var response = client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            Interlocked.Decrement(ref _currentRequests);

            var output = response.Result;

            //dispose
            response.Result.Content.Dispose();
            response.Result.Dispose();
            response.Dispose();

            request.Dispose();

            return output;
        }

        /// <summary>
        /// Perform the last <see cref="ApiRequest"/> on this <see cref="ApiClient"/> again
        /// </summary>
        public T PerformLast<T>() where T : class
        {
            if (CachedRequest == null)
                throw new NullRequestException();

            return Perform<T>(CachedRequest);
        }

        /// <summary>
        /// Perform the last <see cref="ApiRequest"/> on this <see cref="ApiClient"/> again. Returns a <see cref="HttpResponseMessage"/>, where deserializing the data may not be desired
        /// </summary>
        public HttpResponseMessage PerformLast()
        {
            if (CachedRequest == null)
                throw new NullRequestException();

            return Perform(CachedRequest);
        }

        /// <summary>
        /// Download a file with an <see cref="ApiRequest"/>. Incompatible with <see cref="PerformLast{T}"/> and <see cref="ValidateAndProcess{T}"/>
        /// </summary>
        public virtual void Perform(ApiFileRequest requestData)
        {
            //check for nulls
            if (string.IsNullOrWhiteSpace(requestData.Path) || string.IsNullOrWhiteSpace(requestData.Destination))
                throw new NullRequestException();

            //get client and request (disposables)
            var client = GetClient(requestData);
            Interlocked.Increment(ref _currentRequests);

            var request = GetRequest(requestData);

            //post-modification
            SetupRequest(request);

            var response = client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            Interlocked.Decrement(ref _currentRequests);

            //validate
            if (response.Result.IsSuccessStatusCode)
            {
                using (var stream = File.Open(requestData.Destination, FileMode.Create))
                using (var networkStream = response.Result.Content.ReadAsStreamAsync().Result)
                {
                    networkStream.CopyTo(stream);
                }
            }

            //dispose
            response.Result.Content.Dispose();
            response.Result.Dispose();
            response.Dispose();

            request.Dispose();
        }

        /// <summary>
        /// Validates the <see cref="HttpResponseMessage"/> and uses the <see cref="Serializer"/> to deserialize data (if successful)
        /// </summary>
        protected virtual T ValidateAndProcess<T>(Task<HttpResponseMessage> response) where T : class
        {
            if (!response.Result.IsSuccessStatusCode)
                throw new HttpRequestException($"Response was unsuccessful ({response.Result.StatusCode})");

            return Serializer.Deserialize<T>(response.Result.Content.ReadAsStreamAsync());
        }
    }
}
