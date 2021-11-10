using System;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;

namespace Cloudware.Utilities.BusTools
{
    public interface IPublishEndpointInSingletonService
    {
        Task Publish<T>(T message, CancellationToken cancellationToken = default) where T : class;

        Task Publish<T>(T message, Action<PublishContext<T>> callback,
            CancellationToken cancellationToken = default) where T : class;
    }
    public class PublishEndpointInSingletonService : IPublishEndpointInSingletonService
    {
        private readonly IServiceProvider serviceProvider;
        public PublishEndpointInSingletonService(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }
        public async Task Publish<T>(T message, CancellationToken cancellationToken = default) where T : class
        {
            using var scope = serviceProvider.CreateScope();
            var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
            await publishEndpoint.Publish(message, cancellationToken);
        }

        public async Task Publish<T>( T message, Action<PublishContext<T>> callback,
            CancellationToken cancellationToken = default) where T : class
        {
            using var scope = serviceProvider.CreateScope();
            var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
            await publishEndpoint.Publish(message,callback);
        }
    }
}