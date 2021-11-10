using MassTransit.ExtensionsDependencyInjectionIntegration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using MassTransit.RabbitMqTransport;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using System.Text.RegularExpressions;
using GreenPipes;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System.Threading;
using MassTransit.Serialization;

namespace Cloudware.Utilities.BusTools
{
    public static class BusExtensions
    {
        public static void AddRequestClientsClw<RequestType>(this IServiceCollectionMediatorConfigurator configurator, params Assembly[] assemblies)
        {
            var assembly = typeof(RequestType).Assembly;

            IEnumerable<Type> types = assemblies.SelectMany(s => s.GetExportedTypes())
            //IEnumerable<Type> types = assembly.GetExportedTypes()
                .Where(c => c.IsClass && !c.IsAbstract && c.IsPublic && typeof(RequestType).IsAssignableFrom(c)).ToList();
            foreach (Type item in types)
                configurator.AddRequestClient(item);
        }
        public static void AddBusRequestClientsClw<BusRequestClientType>(this IServiceCollectionBusConfigurator configurator, params Assembly[] assemblies)
        {
            IEnumerable<Type> types = assemblies.SelectMany(s => s.GetExportedTypes())
                .Where(w => w.IsClass && !w.IsAbstract && w.IsPublic && typeof(BusRequestClientType).IsAssignableFrom(w)).ToList();
            foreach (var item in types)
                configurator.AddRequestClient(item);
        }
        public static void AddMediatorConsumersClw<MediatorConsumerType>(this IServiceCollectionMediatorConfigurator configurator, params Assembly[] assemblies)
        {
            var assembly = typeof(MediatorConsumerType).Assembly;

            IEnumerable<Type> types = assemblies.SelectMany(s => s.GetExportedTypes())
                //  IEnumerable<Type> types = assemblies.GetExportedTypes()
                .Where(w => w.IsClass && !w.IsAbstract && w.IsPublic && typeof(MediatorConsumerType).IsAssignableFrom(w)).ToList();
            foreach (var item in types)
                configurator.AddConsumer(item);


        }
        public static void AddBusConsumersClw<BusConsumerType>(this IServiceCollectionBusConfigurator configurator, params Assembly[] assemblies)
        {
            var assembly = typeof(BusConsumerType).Assembly;

            IEnumerable<Type> types = assemblies.SelectMany(s => s.GetExportedTypes())
                //  IEnumerable<Type> types = assembly.GetExportedTypes()
                .Where(w => w.IsClass && !w.IsAbstract && w.IsPublic && typeof(BusConsumerType).IsAssignableFrom(w)).ToList();
            foreach (var item in types)
                configurator.AddConsumer(item);


        }
        public static void AddBusMultiConsumersClw<BusMultiConsumerType>(this IRabbitMqBusFactoryConfigurator configurator, IBusRegistrationContext context,string queueName, params Assembly[] assemblies)
        {
            var assembly = typeof(BusMultiConsumerType).Assembly;

            IEnumerable<Type> types = assemblies
                .SelectMany(s => s.GetExportedTypes())
                          //  IEnumerable<Type> types = a.GetExportedTypes()
                          .Where(w => w.IsClass && !w.IsAbstract && w.IsPublic && typeof(BusMultiConsumerType).IsAssignableFrom(w)).ToList();

            configurator.ReceiveEndpoint(queueName, e =>
            {
                foreach (var item in types)
                    e.ConfigureConsumer(context, item);

                //e.confi
            });
        }
        public static void AddRabbitMqClw<BusMultiConsumerType>(this IServiceCollectionBusConfigurator configurator, string hostUrl, string hostUserName, string hostPassword, params Assembly[] assemblies)
        {
            configurator.UsingRabbitMq((context, cfg) =>
            {
                // cfg.UseMessageRetry(r =>
                // {
                //     r.Handle<Exception>();
                //     r.Ignore(typeof(InvalidOperationException), typeof(InvalidCastException));
                //     r.Ignore<ArgumentException>(t => t.ParamName == "orderTotal");
                // });

                cfg.Host(new Uri(hostUrl), d =>
                {
                    if (!string.IsNullOrEmpty(hostUserName))
                    {
                        d.Username(hostUserName);
                    }
                    if (!string.IsNullOrEmpty(hostPassword))
                    {
                        d.Password(hostPassword);
                    }
                });
               // cfg.ClearMessageDeserializers();
                //cfg.SingleActiveConsumer = true;
                // cfg.UseRawJsonSerializer();
               
                var projectAssembly = assemblies 
                  .Where(w => w.GetName().Name.ToLower().StartsWith("cloudware.microservice.") || w.GetName().Name.ToLower().StartsWith("cloudware.worker.") || w.GetName().Name.ToLower().StartsWith("cloudware.orchestrator."))
                  .FirstOrDefault();
                var queueName = ToKebabCase(projectAssembly.GetName().Name);
                cfg.AddBusMultiConsumersClw<BusMultiConsumerType>(context, queueName,assemblies);
                cfg.ConfigurePublish(o =>
                {
                    o.UseExecute(publishContext =>
                    {
                        publishContext.Headers.Set("queue_name",queueName);
                        if (publishContext.TryGetPayload<IServiceProvider>(out var provider))
                        {

                            var ihttpContextAccessor = provider.GetRequiredService<IHttpContextAccessor>();
                            if (ihttpContextAccessor != null)
                            {
                                var tenantid = ihttpContextAccessor.HttpContext?.Request?.Headers["tenant_id"];
                                if (!string.IsNullOrEmpty(tenantid))
                                {
                                    publishContext.Headers.Set("tenant_id", tenantid);
                                }
                                var userId = ihttpContextAccessor.HttpContext?.User?.Claims?.FirstOrDefault(f => f.Type == "sub")?.Value;
                                if (!string.IsNullOrEmpty(userId))
                                {
                                    publishContext.Headers.Set("userid", userId);

                                }
                                var accessToken = ihttpContextAccessor.HttpContext?.Request?.Headers["Authorization"];
                                if (!string.IsNullOrEmpty(accessToken))
                                {
                                    publishContext.Headers.Set("accesstoken", accessToken);
                                }
                            }
                        }
                    });
                });
                cfg.ConfigureEndpoints(context);
            });
        }
        public static void AddMassTransitClw<BusConsumerType, BusMultiConsumerType, BusRequestClientType>(this IServiceCollection services, string hostUrl, string hostUserName, string hostPassword, params Assembly[] assemblies)
        {
            services.AddMassTransit(x =>
            {
                x.AddBusConsumersClw<BusConsumerType>(assemblies);
                x.AddBusRequestClientsClw<BusRequestClientType>(assemblies);
                x.SetKebabCaseEndpointNameFormatter();
                x.AddRabbitMqClw<BusConsumerType>(hostUrl, hostUserName, hostPassword, assemblies);
            }
            );
        }
        public static void AddMassTransitMediatorClw<MediatorConsumerType, RequestType>(this IServiceCollection services, params Assembly[] assemblies)
        {
            services.AddMediator(x =>
            {
                //consumers
                x.AddMediatorConsumersClw<MediatorConsumerType>(assemblies);
                // requestClients
                x.AddRequestClientsClw<RequestType>(assemblies);
            });
        }
        //public class MultiTenantMessageFilter<T> : IFilter<ConsumeContext<T>> where T : class
        //{
        //    ILifetimeScope _mainScope;
        //    private readonly IHttpContextAccessor httpContextAccessor;

        //    public MultiTenantMessageFilter(ILifetimeScope mainScope,IHttpContextAccessor httpContextAccessor)
        //    {
        //        _mainScope = mainScope;
        //        this.httpContextAccessor = httpContextAccessor;
        //    }

        //    public void Probe(ProbeContext context)
        //    {
        //        context.CreateFilterScope("multiTenant");
        //    }

        //    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
        //    {
        //        ILifetimeScope scope = null;

        //        try
        //        {
        //             var a=   httpContextAccessor.HttpContext.Request.Headers["tenann"];
        //            //context.Headers.a
        //            context.GetOrAddPayload<ILifetimeScope>(() =>
        //            {
        //                scope = _mainScope.BeginLifetimeScope();

        //                var guid = scope.Resolve<Guid>();
        //               // var tenantId=HttpContextAccessor
        //                return scope;
        //            });

        //            await next.Send(context).ConfigureAwait(false);
        //        }
        //        finally
        //        {
        //            scope?.Dispose();
        //        }
        //    }
        //}
        public static string ToKebabCase(string value)
        {
            // Replace all non-alphanumeric characters with a dash
            value = Regex.Replace(value, @"[^0-9a-zA-Z]", "-");

            // Replace all subsequent dashes with a single dash
            value = Regex.Replace(value, @"[-]{2,}", "-");

            // Remove any trailing dashes
            value = Regex.Replace(value, @"-+$", string.Empty);

            // Remove any dashes in position zero
            if (value.StartsWith("-")) value = value.Substring(1);

            // Lowercase and return
            return value.ToLower();
        }
    }

    public static class ConsumeContextExtensions
    {
        public static GlobalMessageData GetGlobalMessageData<T>(this ConsumeContext<T> consumeContext) where T : class
        {
            return new GlobalMessageData
            {
                TenantId = Convert.ToString(consumeContext.Headers.FirstOrDefault(f => f.Key == "tenant_id").Value),
                UserId = Convert.ToString(consumeContext.Headers.FirstOrDefault(f => f.Key == "userid").Value),
                AccessToken = Convert.ToString(consumeContext.Headers.FirstOrDefault(f => f.Key == "accesstoken").Value),
            };
        }

        public static string GetDestinationAddress<T>(this ConsumeContext<T> consumeContext) where T : class
        {
            return Convert.ToString(consumeContext.Headers.FirstOrDefault(f => f.Key == "queue_name").Value);
        }
    }
    public class GlobalMessageData
    {
        public string UserId { get; set; }
        public string AccessToken { get; set; }
        public string TenantId { get; set; }
    }
}
