namespace Loupedeck.xkcdviewerPlugin.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    // Stands in for the network: records every request and answers with whatever the test supplies.
    internal sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
        {
            this._respond = respond;
        }

        public List<HttpRequestMessage> Requests { get; } = new List<HttpRequestMessage>();

        public static StubHttpMessageHandler Json(String json) =>
            new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            this.Requests.Add(request);
            return Task.FromResult(this._respond(request));
        }
    }
}
