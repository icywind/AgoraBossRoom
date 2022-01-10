using System;
using System.Collections.Generic;
using UnityEngine.Assertions;

namespace BossRoom.Infrastructure
{
    public interface IPublisher<T>
    {
        void Publish(T message);
    }

    public interface ISubscriber<T>
    {
        IDisposable Subscribe(Action<T> handler);
    }
    
    public class MessageChannel<T> : IDisposable, IPublisher<T>, ISubscriber<T>
    {
        private readonly List<Action<T>> m_MessageHandlers = new List<Action<T>>();

        private readonly Dictionary<Subscription, int> m_HandlerIndices = new Dictionary<Subscription, int>();

        bool m_IsDisposed;
        public void Publish(T message)
        {
            foreach (var messageHandler in m_MessageHandlers)
            {
                messageHandler?.Invoke(message);
            }
        }

        public IDisposable Subscribe(Action<T> handler)
        {
            Assert.IsTrue(!m_MessageHandlers.Contains(handler), $"Attempting to subscribe with the same handler more than once");
            m_MessageHandlers.Add(handler);
            var subscription = new Subscription(this, handler);
            return subscription;
        }

        public void Dispose()
        {
            if (!m_IsDisposed)
            {
                m_IsDisposed = true;
                m_MessageHandlers.Clear();
            }
        }

        private class Subscription : IDisposable
        {
            bool m_isDisposed;
            MessageChannel<T> m_MessageChannel;
            Action<T> m_Handler;

            public Subscription(MessageChannel<T> messageChannel, Action<T> handler)
            {
                m_MessageChannel = messageChannel;
                m_Handler = handler;
            }

            public void Dispose()
            {
                if (!m_isDisposed)
                {
                    m_isDisposed = true;

                    if (!m_MessageChannel.m_IsDisposed)
                    {
                        m_MessageChannel.m_MessageHandlers.Remove(m_Handler);
                    }

                    m_Handler = null;
                    m_MessageChannel = null;
                }
            }
        }
    }
}
