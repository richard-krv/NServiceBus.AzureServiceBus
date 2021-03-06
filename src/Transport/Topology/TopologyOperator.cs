namespace NServiceBus.Transport.AzureServiceBus
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Logging;
    using Microsoft.ServiceBus.Messaging;
    using Settings;

    class TopologyOperator : IOperateTopologyInternal, IDisposable
    {
        public TopologyOperator(MessageReceiverCreator messageReceiverCreator, BrokeredMessagesToIncomingMessagesConverter brokeredMessageConverter, ReadOnlySettings settings)
        {
            this.brokeredMessageConverter = brokeredMessageConverter;
            this.messageReceiverCreator = messageReceiverCreator;

            messageReceiverNotifierSettings = new MessageReceiverNotifierSettings(
                settings.Get<ReceiveMode>(WellKnownConfigurationKeys.Connectivity.MessageReceivers.ReceiveMode),
                settings.HasExplicitValue<TransportTransactionMode>() ? settings.Get<TransportTransactionMode>() : settings.SupportedTransactionMode(),
                settings.Get<TimeSpan>(WellKnownConfigurationKeys.Connectivity.MessageReceivers.AutoRenewTimeout),
                settings.Get<int>(WellKnownConfigurationKeys.Connectivity.NumberOfClientsPerEntity));
        }

        public void Dispose()
        {
            // Injected
        }

        public void Start(TopologySectionInternal topologySection, int maximumConcurrency)
        {
            maxConcurrency = maximumConcurrency;
            topology = topologySection;

            StartNotifiersFor(topology.Entities);

            running = true;

            while (pendingStartOperations.TryTake(out var operation))
            {
                operation();
            }
        }

        public Task Stop()
        {
            logger.Info("Stopping notifiers");
            return StopNotifiersForAsync(topology.Entities);
        }

        public void Start(IEnumerable<EntityInfoInternal> subscriptions)
        {
            if (!running) // cannot start subscribers before the notifier itself is started
            {
                pendingStartOperations.Add(() => StartNotifiersFor(subscriptions));
            }
            else
            {
                StartNotifiersFor(subscriptions);
            }
        }

        public Task Stop(IEnumerable<EntityInfoInternal> subscriptions)
        {
            return StopNotifiersForAsync(subscriptions);
        }

        public void OnIncomingMessage(Func<IncomingMessageDetailsInternal, ReceiveContextInternal, Task> func)
        {
            onMessage = func;
        }

        public void OnError(Func<Exception, Task> func)
        {
            onError = func;
        }

        public void OnCritical(Action<Exception> action)
        {
            onCriticalError = action;
        }

        public void OnProcessingFailure(Func<ErrorContext, Task<ErrorHandleResult>> func)
        {
            onProcessingFailure = func;
        }

        void StartNotifiersFor(IEnumerable<EntityInfoInternal> entities)
        {
            foreach (var entity in entities)
            {
                if (!entity.ShouldBeListenedTo)
                {
                    continue;
                }

                var notifier = notifiers.GetOrAdd(entity, e =>
                {
                    var n = CreateNotifier(entity.Type);
                    n.Initialize(e, onMessage, onError, onCriticalError, onProcessingFailure, maxConcurrency);
                    return n;
                });

                notifier.Start();
            }
        }

        INotifyIncomingMessagesInternal CreateNotifier(EntityType type)
        {
            if (type == EntityType.Queue || type == EntityType.Subscription)
            {
                return new MessageReceiverNotifier(messageReceiverCreator, brokeredMessageConverter, messageReceiverNotifierSettings);
            }

            throw new NotSupportedException("Entity type " + type + " not supported");
        }

        async Task StopNotifiersForAsync(IEnumerable<EntityInfoInternal> entities)
        {
            foreach (var entity in entities)
            {
                notifiers.TryGetValue(entity, out var notifier);

                if (notifier == null)
                {
                    continue;
                }

                await notifier.Stop().ConfigureAwait(false);
            }
        }

        TopologySectionInternal topology;

        Func<IncomingMessageDetailsInternal, ReceiveContextInternal, Task> onMessage;
        Func<Exception, Task> onError;
        Func<ErrorContext, Task<ErrorHandleResult>> onProcessingFailure;

        ConcurrentDictionary<EntityInfoInternal, INotifyIncomingMessagesInternal> notifiers = new ConcurrentDictionary<EntityInfoInternal, INotifyIncomingMessagesInternal>();

        volatile bool running;
        ConcurrentBag<Action> pendingStartOperations = new ConcurrentBag<Action>();
        ILog logger = LogManager.GetLogger(typeof(TopologyOperator));

        int maxConcurrency;
        MessageReceiverCreator messageReceiverCreator;
        BrokeredMessagesToIncomingMessagesConverter brokeredMessageConverter;
        MessageReceiverNotifierSettings messageReceiverNotifierSettings;
        Action<Exception> onCriticalError;
    }
}