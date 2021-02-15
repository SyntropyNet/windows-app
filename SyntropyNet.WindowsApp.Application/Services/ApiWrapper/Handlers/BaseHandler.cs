using Websocket.Client;

namespace SyntropyNet.WindowsApp.Application.Services.ApiWrapper.Handlers
{
    public abstract class BaseHandler
    {
        protected WebsocketClient Client;

        protected BaseHandler(WebsocketClient client)
        {
            this.Client = client;
        }
    }
}