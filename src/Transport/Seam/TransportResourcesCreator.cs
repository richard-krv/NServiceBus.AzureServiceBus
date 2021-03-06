namespace NServiceBus.Transport.AzureServiceBus
{
    using System.Threading.Tasks;

    class TransportResourcesCreator : ICreateQueues
    {
        public TransportResourcesCreator(TopologyCreator topologyCreator, ITopologySectionManagerInternal sections, string localAddress)
        {
            this.topologyCreator = topologyCreator;
            this.sections = sections;
            this.localAddress = localAddress;
        }

        public async Task CreateQueueIfNecessary(QueueBindings queueBindings, string identity)
        {
            if (resourcesCreated)
            {
                return;
            }

            await topologyCreator.AssertManagedRights().ConfigureAwait(false);

            await sections.Initialize().ConfigureAwait(false);
            var queuesToCreate = sections.DetermineQueuesToCreate(queueBindings, localAddress);
            await topologyCreator.Create(queuesToCreate).ConfigureAwait(false);

            resourcesCreated = true;
        }

        ITopologySectionManagerInternal sections;
        readonly string localAddress;
        TopologyCreator topologyCreator;
        bool resourcesCreated;
    }
}