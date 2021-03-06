namespace NServiceBus.Transport.AzureServiceBus
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    interface IRouteOutgoingBatchesInternal
    {
        Task RouteBatches(IEnumerable<BatchInternal> outgoingBatches, ReceiveContextInternal receiveContext, DispatchConsistency consistency);
    }
}