using System;
using System.Threading;
using System.Threading.Tasks;

namespace Perper.WebJobs.Extensions.Model
{
    public interface IPerperStreamContext
    {
        Task<T> FetchStateAsync<T>();
        Task UpdateStateAsync<T>(T state);
        Task<T> CallWorkerAsync<T>(object parameters, CancellationToken cancellationToken);

        Task<IAsyncDisposable> StreamActionAsync(string name, object parameters);
        Task<IAsyncDisposable> StreamFunctionAsync(string name, object parameters);

        Task WaitUntilCancelled(CancellationToken cancellationToken);
    }
}