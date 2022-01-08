using System;
using System.Collections.Generic;

namespace BossRoom.Infrastructure
{
    public class DisposableGroup : IDisposable
    {
        private List<IDisposable> m_Disposables = new List<IDisposable>();
        public void Add(IDisposable disposable)
        {
            m_Disposables.Add(disposable);
        }

        public void Dispose()
        {
            foreach (var disposable in m_Disposables)
            {
                disposable.Dispose();
            }
            m_Disposables.Clear();
        }
    }
}
