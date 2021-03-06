﻿namespace NServiceBus.Azure.WindowsAzureServiceBus.Tests.API
{
    using NUnit.Framework;
    using Particular.Approvals;
    using PublicApiGenerator;

    [TestFixture]
    public class APIApprovals
    {
        [Test]
        public void ApproveAzureServiceBusTransport()
        {
            var publicApi = ApiGenerator.GeneratePublicApi(typeof(AzureServiceBusTransport).Assembly);
            Approver.Verify(publicApi);
        }
    }
}