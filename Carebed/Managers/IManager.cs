using Carebed.Infrastructure.EventBus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Carebed.Managers
{
    /// <summary>
    /// Minimal lifecycle contract for application managers.
    /// </summary>
    public interface IManager : IDisposable
    {
        void Start();
        void Stop();
    }
}
