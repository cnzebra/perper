using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Apache.Ignite.Core.Binary;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Protocols;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Perper.WebJobs.Extensions.Model;
using Perper.WebJobs.Extensions.Services;

namespace Perper.WebJobs.Extensions.Triggers
{
    public class PerperStreamTriggerBinding : ITriggerBinding
    {
        private readonly PerperFabricContext _fabricContext;
        private readonly IBinary _binary;

        public PerperStreamTriggerBinding(PerperFabricContext fabricContext, IBinary binary)
        {
            _fabricContext = fabricContext;
            _binary = binary;
        }
        
        public Task<ITriggerData> BindAsync(object value, ValueBindingContext context)
        {
            throw new NotImplementedException();
        }

        public Task<IListener> CreateListenerAsync(ListenerFactoryContext context)
        {
            return Task.FromResult<IListener>(new PerperStreamListener(context.Descriptor.ShortName, _fabricContext,
                _binary, context.Executor));
        }

        public ParameterDescriptor ToParameterDescriptor()
        {
            return new ParameterDescriptor();
        }

        public Type TriggerValueType => typeof(IPerperStreamContext);
        public IReadOnlyDictionary<string, Type> BindingDataContract { get; } = new Dictionary<string, Type>();
    }
}