﻿// Copyright (c) 2017 TPDT
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
// 
// SimCivil - SimCivil.Rpc - RpcClient.cs
// Create Date: 2019/05/08
// Update Date: 2019/05/19

using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Castle.DynamicProxy;

using DotNetty.Codecs;
using DotNetty.Handlers.Timeout;
using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;

using SimCivil.Contract;
using SimCivil.Rpc.Callback;
using SimCivil.Rpc.Timeout;

namespace SimCivil.Rpc
{
    public class RpcClient : IDisposable
    {
        private readonly IChannelHandler _callbackResolver;
        private readonly IChannelHandler _decoder = new JsonToMessageDecoder();
        private readonly IChannelHandler _encoder = new MessageToJsonEncoder<RpcRequest>();

        private readonly ProxyGenerator _generator = new ProxyGenerator();
        private readonly IChannelHandler _resolver;
        private readonly IConnectionControl _connectionControl;
        private int _nextCallbackId;

        private long _nextSeq;

        public IPEndPoint EndPoint { get; private set; }
        public IChannel Channel { get; private set; }
        public Dictionary<Type, object> ProxyCache { get; } = new Dictionary<Type, object>();
        public Dictionary<long, RpcRequest> ResponseWaitlist { get; } = new Dictionary<long, RpcRequest>();
        public int ResponseTimeout { get; set; } = 3000;
        /// <summary>
        /// Gets or sets the heartbeat delay in miliseconds.
        /// </summary>
        /// <value>
        /// The heartbeat delay.
        /// </value>
        public int HeartbeatDelay { get; set; } = 2000;
        public IInterceptor Interceptor { get; }
        public bool Connected { get; private set; }

        public Dictionary<int, Delegate> CallBackList { get; } = new Dictionary<int, Delegate>();

        public RpcClient() : this(new IPEndPoint(IPAddress.Loopback, 20170)) { }

        public RpcClient(IPEndPoint endPoint)
        {
            EndPoint = endPoint;
            Interceptor = new RpcInterceptor(this);
            _resolver = new RpcClientResolver(this);
            _callbackResolver = new RpcCallbackResolver(this);
            _connectionControl = Import<IConnectionControl>();
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (Channel?.Open ?? false)
                Channel.CloseAsync().Wait();
        }

        public event EventHandler<EventArgs<string>> DecodeFail;

        public RpcClient Bind(int port)
        {
            EndPoint = new IPEndPoint(IPAddress.Loopback, port);

            return this;
        }

        public RpcClient Bind(string ip, int port)
        {
            EndPoint = new IPEndPoint(IPAddress.Parse(ip), port);

            return this;
        }

        public async Task ConnectAsync()
        {
            if (EndPoint == null)
                throw new InvalidOperationException(nameof(EndPoint));
            if (Channel?.Open ?? false)
                throw new InvalidOperationException(nameof(Channel));

            IEventLoopGroup loopGroup = new MultithreadEventLoopGroup();
            try
            {
                var bootstrap = new Bootstrap();
                bootstrap.Group(loopGroup)
                         .Channel<TcpSocketChannel>()
                         .Option(ChannelOption.SoKeepalive, true)
                         .Option(ChannelOption.TcpNodelay, true)
                         .Handler(
                              new ActionChannelInitializer<ISocketChannel>(
                                  ChannelInit));
                Channel = await bootstrap.ConnectAsync(EndPoint);
            }
            catch
            {
                Channel?.CloseAsync().Wait();
                loopGroup.ShutdownGracefullyAsync().Wait();

                throw;
            }
            
            Connected = true;
        }

        protected virtual void ChannelInit(ISocketChannel channel)
        {
            channel.Pipeline
                   .AddLast(new IdleStateHandler(0, HeartbeatDelay / 1000, 0))
                   .AddLast(new ClientIdleHandler(SendHeartbeat))
                   .AddLast(new LengthFieldPrepender(2))
                   .AddLast(new LengthFieldBasedFrameDecoder(ushort.MaxValue, 0, 2, 0, 2))
                   .AddLast(_decoder)
                   .AddLast(_encoder)
                   .AddLast(_resolver)
                   .AddLast(_callbackResolver);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Disconnect()
        {
            if (Connected)
            {
                Channel?.DisconnectAsync();
                Connected = false;
                Channel = null;
                ProxyCache.Clear();
            }
        }

        /// <summary>
        /// Imports or gets remote service.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T Import<T>() where T : class
        {
            if (ProxyCache.TryGetValue(typeof(T), out object service)) return service as T;

            var newService = _generator.CreateInterfaceProxyWithoutTarget<T>(Interceptor);
            ProxyCache[typeof(T)] = newService;

            return newService;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public long GetNextSequence() => _nextSeq++;

        protected virtual void OnDecodeFail(EventArgs<string> e)
        {
            DecodeFail?.Invoke(this, e);
        }

        /// <summary>Attaches the callback and gets id.</summary>
        /// <param name="delegate">The callback to be attached.</param>
        /// <returns>Id of attached callback</returns>
        public int AttachCallback(Delegate @delegate)
        {
            int id = Interlocked.Increment(ref _nextCallbackId);
            CallBackList[id] = @delegate;

            return id;
        }

        public void SendHeartbeat()
        {
            Task.Run(
                () =>
                {
                    try
                    {
                        _connectionControl.Noop();
                    }
                    catch
                    {
                        // Heartbeat failed
                        Disconnect();
                    }
                });
        }

        public void NotifyPacketSent()
        {
            if (!Connected)
                throw new InvalidOperationException("Rpc Client is disconnected");
        }
    }

}