using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Log.It;
using Port.Server.IntegrationTests.TestFramework;
using Port.Server.Kubernetes;
using Port.Server.Spdy;
using Port.Server.Spdy.Collections;
using Port.Server.Spdy.Frames;
using Port.Server.Spdy.Primitives;
using Xunit;
using Xunit.Abstractions;

namespace Port.Server.IntegrationTests
{
    public class UnitTest1 : TestSpecificationAsync
    {
        public UnitTest1(
            ITestOutputHelper testOutputHelper)
            : base(testOutputHelper)
        {
        }

        [Fact(Skip = "Dependent on a real k8s cluster and services")]
        public async Task Test1()
        {
            var factory =
                new KubernetesClientFactory(new KubernetesConfiguration());
            var ks = new KubernetesService(
                factory, new SocketNetworkServerFactory(), new TestFeatureManager());
            await ks.PortForwardAsync(
                        "kind-argo-demo-ci", new Shared.PortForward(
                                podPort: 2746,
                                protocolType: ProtocolType.Tcp,
                                @namespace: "argo",
                                service: "argo-server-5f5c647dcb-bkcz6",
                                pod: "")
                        { LocalPort = 2746 }, CancellationTokenSource.Token)
                    .ConfigureAwait(false);

            await Task.Delay(int.MaxValue)
                      .ConfigureAwait(false);
        }

        [Fact(Skip = "Dependent on a real k8s cluster and services")]
        public async Task TestWithSpdy()
        {
            var config = new KubernetesConfiguration();
            var factory =
                new KubernetesClientFactory(config);
            await using var ks = new KubernetesService(
                factory, new SocketNetworkServerFactory(), new TestFeatureManager((nameof(Features.PortForwardingWithSpdy), true)));
            await ks.PortForwardAsync(
                        "kind-argo-demo-test", new Shared.PortForward(
                                podPort: 80,
                                protocolType: ProtocolType.Tcp,
                                @namespace: "argocd",
                                service: "",
                                pod: "argocd-server-78ffb87fd8-f6559")
                        { LocalPort = 8081 }, CancellationTokenSource.Token)
                    .ConfigureAwait(false);

            await Task.Delay(5000)
                      .ConfigureAwait(false);
        }

        [Fact(Skip = "Dependent on a real k8s cluster and services")]
        public async Task TestWithSpdySession()
        {
            var podPort = 80;
            var config = new KubernetesConfiguration();
            var factory =
                new KubernetesClientFactory(config);
            using var client = factory.Create("kind-argo-demo-test");
            var session = await client.SpdyNamespacedPodPortForwardAsync(
                                          "argocd-server-78ffb87fd8-f6559",
                                          "argocd",
                                          new[] { podPort },
                                          CancellationToken.None)
                                      .ConfigureAwait(false);

            using var stream = session.Open(
                headers: new NameValueHeaderBlock(
                    (Kubernetes.Headers.StreamType, new[]
                    {
                        Kubernetes.Headers.StreamTypeData
                    }),
                    (Kubernetes.Headers.Port, new[]
                    {
                        podPort.ToString()
                    })));
            await stream.Remote.WaitForOpenedAsync()
                        .ConfigureAwait(false);
            await Task.Delay(10)
                      .ConfigureAwait(false);
        }
    }
}