using System;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Newtonsoft.Json;
using WalletConnectSharp.Core.Events;
using WalletConnectSharp.Core.Events.Request;
using WalletConnectSharp.Core.Events.Response;
using WalletConnectSharp.Core.Models;
using WalletConnectSharp.Core.Network;
using Websocket.Client;
using Websocket.Client.Models;

namespace WalletConnectSharp.Desktop.Network
{
    public class WebsocketTransport : ITransport, IObserver<ResponseMessage>, IObserver<DisconnectionInfo>
    {
        private WebsocketClient client;
        private EventDelegator _eventDelegator;

        public WebsocketTransport(EventDelegator eventDelegator)
        {
            this._eventDelegator = eventDelegator;
        }

        public void Dispose()
        {
            if (client != null)
                client.Dispose();
        }

        public event EventHandler<MessageReceivedEventArgs> MessageReceived;
        public async Task Open(string url)
        {
            if (url.StartsWith("https"))
                url = url.Replace("https", "wss");
            else if (url.StartsWith("http"))
                url = url.Replace("http", "ws");
            
            if (client != null)
                return;
            
            client = new WebsocketClient(new Uri(url));
            
            client.MessageReceived.Subscribe(this);
            client.DisconnectionHappened.Subscribe(this);

            //TODO Log this
            /*client.ReconnectionHappened.Subscribe(delegate(ReconnectionInfo info)
            {
                Console.WriteLine(info.Type);
            });*/

            await client.Start();
        }

        public async Task Close()
        {
            await client.Stop(WebSocketCloseStatus.NormalClosure, "");
        }

        public async Task SendMessage(NetworkMessage message)
        {
            var finalJson = JsonConvert.SerializeObject(message);
            
            await this.client.SendInstant(finalJson);
        }

        public async Task Subscribe(string topic)
        {
            await SendMessage(new NetworkMessage()
            {
                Payload = "",
                Type = "sub",
                Silent = true,
                Topic = topic
            });
        }

        public async Task Subscribe<T>(string topic, EventHandler<JsonRpcResponseEvent<T>> callback) where T : JsonRpcResponse
        {
            await Subscribe(topic);

            _eventDelegator.ListenFor(topic, callback);
        }
        
        public async Task Subscribe<T>(string topic, EventHandler<JsonRpcRequestEvent<T>> callback) where T : JsonRpcRequest
        {
            await Subscribe(topic);

            _eventDelegator.ListenFor(topic, callback);
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
            throw error;
        }

        public async void OnNext(ResponseMessage responseMessage)
        {
            var json = responseMessage.Text;

            var msg = JsonConvert.DeserializeObject<NetworkMessage>(json);

            await SendMessage(new NetworkMessage()
            {
                Payload = "",
                Type = "ack",
                Silent = true,
                Topic = msg.Topic
            });


            if (this.MessageReceived != null)
            {
                MessageReceived(this, new MessageReceivedEventArgs(msg, this));
            }
        }

        public void OnNext(DisconnectionInfo value)
        {
            client.Reconnect();
        }
    }
}