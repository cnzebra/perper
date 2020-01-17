using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Apache.Ignite.Core.Binary;
using Apache.Ignite.Core.Client;
using Microsoft.Extensions.Logging;
using Perper.Protocol.Cache;

namespace Perper.WebJobs.Extensions.Services
{
    public class PerperFabricData
    {
        private readonly string _streamName;
        private readonly IIgniteClient _igniteClient;
        private readonly ILogger _logger;

        public PerperFabricData(string streamName, IIgniteClient igniteClient, ILogger logger)
        {
            _logger = logger;
            _streamName = streamName;
            _igniteClient = igniteClient;
        }

        public async Task<IAsyncDisposable> StreamFunctionAsync(string name, object parameters)
        {
            var typeName = new StreamBinaryTypeName(GenerateName(name), name, DelegateType.Function);
            var streamsCacheClient = _igniteClient.GetBinaryCache<string>("streams");
            await streamsCacheClient.PutAsync(typeName.StreamName, CreateProtocolObject(typeName, parameters));
            return new PerperFabricStream(typeName, _igniteClient);
        }

        public async Task<IAsyncDisposable> StreamActionAsync(string name, object parameters)
        {
            var typeName = new StreamBinaryTypeName(GenerateName(name), name, DelegateType.Action);
            var streamsCacheClient = _igniteClient.GetBinaryCache<string>("streams");
            await streamsCacheClient.PutAsync(typeName.StreamName, CreateProtocolObject(typeName, parameters));
            return new PerperFabricStream(typeName, _igniteClient);
        }

        public async Task BindStreamOutputAsync(IEnumerable<IAsyncDisposable> streams)
        {
            var binary = _igniteClient.GetBinary();
            var streamsObjects = streams.Select(s =>
                binary.GetBuilder(((PerperFabricStream) s).TypeName.ToString()).Build()).ToArray();

            if (streamsObjects.Any())
            {
                var streamsCacheClient = _igniteClient.GetBinaryCache<string>("streams");
                var streamObject = await streamsCacheClient.GetAsync(_streamName);
                var updatedStreamObject = streamObject.ToBuilder().SetField("$return", streamsObjects).Build();
                await streamsCacheClient.ReplaceAsync(_streamName, updatedStreamObject);
            }
        }

        public async Task<T> FetchStreamParameterAsync<T>(string name)
        {
            var streamsCacheClient = _igniteClient.GetBinaryCache<string>("streams");
            var streamObject = await streamsCacheClient.GetAsync(_streamName);
            var field = streamObject.GetField<object>(name);

            if (!streamObject.HasField(name) && name != "context")
            {
                _logger.LogWarning($"No value found for parameter '{name}' of stream '{_streamName}'");
            }
            else if (field == null && name != "context")
            {
                _logger.LogWarning($"Null or mismatching type passed for parameter '{name}' of stream '{_streamName}'");
            }

            if (field is IBinaryObject binaryObject)
            {
                return binaryObject.Deserialize<T>();
            }

            return (T)field;
        }

        public async Task UpdateStreamParameterAsync<T>(string name, T value)
        {
            var streamsCacheClient = _igniteClient.GetBinaryCache<string>("streams");
            var streamObject = await streamsCacheClient.GetAsync(_streamName);
            var updatedStreamObject = streamObject.ToBuilder().SetField(name, value).Build();
            await streamsCacheClient.ReplaceAsync(_streamName, updatedStreamObject);
        }

        public async Task AddStreamItemAsync<T>(T value)
        {
            var streamCacheClient = _igniteClient.GetCache<long, T>(_streamName);
            await streamCacheClient.PutAsync(DateTime.UtcNow.ToFileTimeUtc(), value);
        }

        public async Task<T> FetchStreamParameterStreamItemAsync<T>(string itemStreamName, long itemKey)
        {
            var itemStreamCacheClient = _igniteClient.GetCache<long, T>(itemStreamName);
            return await itemStreamCacheClient.GetAsync(itemKey);
        }

        public async Task<string> CallWorkerAsync(string name, object parameters)
        {
            var typeName = new WorkerBinaryTypeName(GenerateName(name), name);
            var workersCache = _igniteClient.GetBinaryCache<string>($"{_streamName}_workers");
            await workersCache.PutAsync(typeName.WorkerName, CreateProtocolObject(typeName, parameters));
            return typeName.WorkerName;
        }

        public async Task<T> FetchWorkerParameterAsync<T>(string workerName, string name)
        {
            var workersCache = _igniteClient.GetBinaryCache<string>($"{_streamName}_workers");
            var workerObject = await workersCache.GetAsync(workerName);
            var field = workerObject.GetField<object>(name);

            if (field is IBinaryObject binaryObject)
            {
                return binaryObject.Deserialize<T>();
            }

            return (T)field;
        }

        public async Task SubmitWorkerResultAsync<T>(string workerName, T value)
        {
            var workersCache = _igniteClient.GetBinaryCache<string>($"{_streamName}_workers");
            var workerObject = await workersCache.GetAsync(workerName);
            var updatedWorkerObject = workerObject.ToBuilder().SetField("$return", value).Build();
            await workersCache.ReplaceAsync(workerName, updatedWorkerObject);
        }

        public async Task<T> ReceiveWorkerResultAsync<T>(string workerName)
        {
            var workersCache = _igniteClient.GetBinaryCache<string>($"{_streamName}_workers");
            var workerObject = await workersCache.GetAndRemoveAsync(workerName);
            var field = workerObject.Value.GetField<object>("$return");
            if (field is IBinaryObject binaryObject)
            {
                return binaryObject.Deserialize<T>();
            }

            return (T)field;
        }

        private static string GenerateName(string delegateName)
        {
            return $"{delegateName.Replace("'", "").Replace(",", "")}-{Guid.NewGuid().ToString()}";
        }

        private IBinaryObject CreateProtocolObject(object header, object parameters)
        {
            var binary = _igniteClient.GetBinary();
            var builder = _igniteClient.GetBinary().GetBuilder(header.ToString());

            var properties = parameters.GetType().GetProperties();
            foreach (var propertyInfo in properties)
            {
                var propertyValue = propertyInfo.GetValue(parameters);
                switch (propertyValue)
                {
                    case PerperFabricStream stream:
                        builder.SetField(propertyInfo.Name, new[] {binary.GetBuilder(stream.TypeName.ToString()).Build()});
                        break;
                    case IAsyncDisposable[] streams when streams.FirstOrDefault() is PerperFabricStream:
                        builder.SetField(propertyInfo.Name, streams.Select(s =>
                            binary.GetBuilder(((PerperFabricStream) s).TypeName.ToString()).Build()).ToArray());
                        break;
                    default:
                        builder.SetField(propertyInfo.Name, propertyValue);
                        break;
                }
            }

            return builder.Build();
        }
    }
}