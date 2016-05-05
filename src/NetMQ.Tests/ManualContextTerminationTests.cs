using System;
using NetMQ.Sockets;
using NUnit.Framework;

namespace NetMQ.Tests
{
    [TestFixture]
    public class ManualContextTerminationTests
    {
        [TestFixtureSetUp]
        public void Setup()
        {
            NetMQConfig.BlockTermination = true;
            NetMQConfig.ManualTerminationTakeOver();
        }

        [TestFixtureTearDown]
        public void Cleanup()
        {
            NetMQConfig.BlockTermination = false;
            NetMQConfig.DisableManualTermination();
        }

        [Test(Description = "When ManualTerminationTakeOver is not called the Context shouldn't be terminated.")]
        public void CallTerminateDefaultBehavior()
        {
            NetMQConfig.DisableManualTermination();
            NetMQConfig.ContextTerminate();
            var isTerminated = VerifyTermination();

            // Restore default test behavior
            NetMQConfig.ManualTerminationTakeOver();
            Assert.AreEqual(false, isTerminated);
        }

        [Test]
        public void CreateContext()
        {
            NetMQConfig.ContextCreate();
            var isTerminated = VerifyTermination();
            Assert.AreEqual(false, isTerminated);
        }

        [Test]
        public void TerminateAfterSocketsUse()
        {
            NetMQConfig.ContextCreate();
            using (var rep = new ResponseSocket())
            using (var req = new RequestSocket())
            {
                var port = rep.BindRandomPort("tcp://127.0.0.1");
                req.Connect("tcp://127.0.0.1:" + port);

                req.SendFrame("Hi");

                CollectionAssert.AreEqual(new[] { "Hi" }, rep.ReceiveMultipartStrings());

                rep.SendFrame("Hi2");

                CollectionAssert.AreEqual(new[] { "Hi2" }, req.ReceiveMultipartStrings());
            }
            NetMQConfig.ContextTerminate();
            var isTerminated = VerifyTermination();
            Assert.AreEqual(true, isTerminated);
        }

        /// <summary>
        /// Test case from <see cref="BeaconTests.SimplePublishSubscribe"/>, blocks context termination.
        /// </summary>
        [Test]
        public void TerminateAfterBeaconUse()
        {
            using (var speaker = new NetMQBeacon())
            using (var listener = new NetMQBeacon())
            {
                speaker.Configure(9999);

                speaker.Publish("Hello", TimeSpan.FromMilliseconds(100));

                listener.Configure(9999);
                listener.Subscribe("H");

                var message = listener.Receive();

                Console.WriteLine(message.PeerAddress);

                Assert.AreEqual("Hello", message.String);
            }
            NetMQConfig.ContextTerminate();
            var isTerminated = VerifyTermination();
            Assert.AreEqual(true, isTerminated);
        }

        [Test, Repeat(10)]
        public void CycleCreateTerminate()
        {
            NetMQConfig.ContextCreate();
            var isTerminated = VerifyTermination();
            Assert.AreEqual(false, isTerminated);

            // We use the Poller Test code.
            using (var rep = new ResponseSocket())
            using (var req = new RequestSocket())
            using (var poller = new NetMQPoller { rep })
            {
                var port = rep.BindRandomPort("tcp://127.0.0.1");

                req.Connect("tcp://127.0.0.1:" + port);

                rep.ReceiveReady += (s, e) =>
                {
                    bool more;
                    Assert.AreEqual("Hello", e.Socket.ReceiveFrameString(out more));
                    Assert.False(more);

                    e.Socket.SendFrame("World");
                };

                poller.RunAsync();

                req.SendFrame("Hello");

                bool more2;
                Assert.AreEqual("World", req.ReceiveFrameString(out more2));
                Assert.IsFalse(more2);

                poller.Stop();
            }
            NetMQConfig.ContextTerminate();
            isTerminated = VerifyTermination();
            Assert.AreEqual(true, isTerminated);
        }

        [Test]
        public void TerminateContext()
        {
            NetMQConfig.ContextCreate();
            NetMQConfig.ContextTerminate();
            var isTerminated = VerifyTermination();
            Assert.AreEqual(true, isTerminated);
        }

        #region Helpers

        private static bool VerifyTermination()
        {
            var isTerminated = false;
            try
            {
                NetMQConfig.Context.CheckDisposed();
            }
            catch (ObjectDisposedException)
            {
                isTerminated = true;
            }

            return isTerminated;
        }

        #endregion

    }
}