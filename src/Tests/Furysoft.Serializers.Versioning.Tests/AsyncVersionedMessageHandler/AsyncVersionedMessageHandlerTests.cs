﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="AsyncVersionedMessageHandlerTests.cs" company="Simon Paramore">
// © 2017, Simon Paramore
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace Furysoft.Serializers.Versioning.Tests.AsyncVersionedMessageHandler
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Furysoft.Serializers.Entities;
    using Furysoft.Serializers.Versioning.Handlers;
    using Furysoft.Serializers.Versioning.Tests.TestEntities;
    using Furysoft.Versioning;
    using NUnit.Framework;

    /// <summary>
    /// The AsyncVersionedMessageHandler Tests.
    /// </summary>
    [TestFixture]
    public sealed class AsyncVersionedMessageHandlerTests : TestBase
    {
        /// <summary>
        /// Versioned the message handler when batched versioned message expect all processed.
        /// </summary>
        /// <returns>The <see cref="Task"/>.</returns>
        [Test]
        public async Task VersionedMessageHandler_WhenBatchedVersionedMessage_ExpectAllProcessed()
        {
            // Arrange
            var entityOne = default(TestEntityOne);
            var entityTwo = default(TestEntityTwo);
            var defaultValue = default(string);
            var exception = default(Exception);

            var versionedMessageHandler = new AsyncVersionedMessageHandler(SerializerType.ProtocolBuffers, true)
                .On<TestEntityOne>(
                    e =>
                    {
                        entityOne = e;
                        return Task.CompletedTask;
                    })
                .On<TestEntityTwo>(
                    e =>
                    {
                        entityTwo = e;
                        return Task.CompletedTask;
                    })
                .Else(
                    s =>
                    {
                        defaultValue = s;
                        return Task.CompletedTask;
                    })
                .OnError(
                    e =>
                    {
                        exception = e;
                        return Task.CompletedTask;
                    });

            var batchedVersionedMessage = new BatchedVersionedMessage
            {
                Messages = new List<VersionedMessage>
                {
                    new TestEntityOne { Value1 = "test", Value2 = 42 }.SerializeToVersionedMessage(),
                    new TestEntityTwo { Value1 = "Value1", Value2 = new DateTime(2018, 1, 1) }.SerializeToVersionedMessage(),
                    new TestEntityThree { Value1 = 3 }.SerializeToVersionedMessage(),
                },
            };

            // Act
            var stopwatch = Stopwatch.StartNew();
            await versionedMessageHandler.PostAsync(batchedVersionedMessage).ConfigureAwait(false);
            stopwatch.Stop();

            // Assert
            this.WriteTimeElapsed(stopwatch);

            Assert.That(entityOne, Is.Not.Null);
            Assert.That(entityTwo, Is.Not.Null);
            Assert.That(defaultValue, Is.Not.Null);
            Assert.That(exception, Is.Null);

            Assert.That(entityOne.Value1, Is.EqualTo("test"));
            Assert.That(entityOne.Value2, Is.EqualTo(42));

            Assert.That(entityTwo.Value1, Is.EqualTo("Value1"));
            Assert.That(entityTwo.Value2, Is.EqualTo(new DateTime(2018, 1, 1)));

            Assert.That(defaultValue.Deserialize<TestEntityThree>().Value1, Is.EqualTo(3));
        }

        /// <summary>
        /// Versioned the message handler when different serializations expect all processed.
        /// </summary>
        /// <returns>
        /// The <see cref="Task" />.
        /// </returns>
        [Test]
        public async Task VersionedMessageHandler_WhenDifferentSerializations_ExpectAllProcessed()
        {
            // Arrange
            var responses = new List<TestEntityOne>();

            var versionedMessageHandler = new AsyncVersionedMessageHandler(SerializerType.ProtocolBuffers, true)
                .On<TestEntityOne>(
                    e =>
                    {
                        responses.Add(e);
                        return Task.CompletedTask;
                    });

            var e1 = new TestEntityOne { Value1 = "test1", Value2 = 42 }.SerializeToVersionedMessage();
            var e2 = new TestEntityOne { Value1 = "test2", Value2 = 42 }.SerializeToVersionedMessage();
            var e3 = new TestEntityOne { Value1 = "test3", Value2 = 42 }.SerializeToVersionedMessage(SerializerType.Json);
            var e4 = new TestEntityOne { Value1 = "test4", Value2 = 42 }.SerializeToVersionedMessage(SerializerType.Xml);

            // Act
            var stopwatch = Stopwatch.StartNew();
            await versionedMessageHandler.PostAsync(e1).ConfigureAwait(false);
            await versionedMessageHandler.PostAsync(e2, SerializerType.ProtocolBuffers).ConfigureAwait(false);
            await versionedMessageHandler.PostAsync(e3, SerializerType.Json).ConfigureAwait(false);
            await versionedMessageHandler.PostAsync(e4, SerializerType.Xml).ConfigureAwait(false);
            stopwatch.Stop();

            // Assert
            this.WriteTimeElapsed(stopwatch);

            Assert.That(responses.Count, Is.EqualTo(4));

            Assert.That(responses[0].Value1, Is.EqualTo("test1"));
            Assert.That(responses[1].Value1, Is.EqualTo("test2"));
            Assert.That(responses[2].Value1, Is.EqualTo("test3"));
            Assert.That(responses[3].Value1, Is.EqualTo("test4"));
        }

        /// <summary>
        /// Versioned the message handler when error and not throw on error expect handled.
        /// </summary>
        /// <returns>The <see cref="Task"/>.</returns>
        [Test]
        public async Task VersionedMessageHandler_WhenErrorAndNotThrowOnError_ExpectHandled()
        {
            // Arrange
            var entityTwo = default(TestEntityTwo);
            var defaultValue = default(string);
            var exception = default(Exception);

            var versionedMessageHandler = new AsyncVersionedMessageHandler(SerializerType.Json, false)
                .On<TestEntityOne>(e => throw new DivideByZeroException())
                .On<TestEntityTwo>(
                    e =>
                    {
                        entityTwo = e;
                        return Task.CompletedTask;
                    })
                .Else(
                    s =>
                    {
                        defaultValue = s;
                        return Task.CompletedTask;
                    })
                .OnError(
                    e =>
                    {
                        exception = e;
                        return Task.CompletedTask;
                    });

            var versionedMessage = new VersionedMessage
            {
                Data = new TestEntityOne().SerializeToString(SerializerType.Json),
                Version = typeof(TestEntityOne).GetVersion(),
            };

            // Act
            var stopwatch = Stopwatch.StartNew();
            await versionedMessageHandler.PostAsync(versionedMessage).ConfigureAwait(false);
            stopwatch.Stop();

            // Assert
            this.WriteTimeElapsed(stopwatch);

            Assert.That(entityTwo, Is.Null);
            Assert.That(defaultValue, Is.Null);
            Assert.That(exception, Is.Not.Null);

            Assert.That(exception.GetType(), Is.EqualTo(typeof(DivideByZeroException)));
        }

        /// <summary>
        /// Versioned the message handler when error and throw on error expect throws.
        /// </summary>
        [Test]
        public void VersionedMessageHandler_WhenErrorAndThrowOnError_ExpectThrows()
        {
            // Arrange
            var versionedMessageHandler = new AsyncVersionedMessageHandler(SerializerType.Json, true)
                .On<TestEntityOne>(e => throw new DivideByZeroException())
                .On<TestEntityTwo>(
                    e =>
                    {
                        return Task.CompletedTask;
                    })
                .Else(
                    s =>
                    {
                        return Task.CompletedTask;
                    })
                .OnError(
                    e =>
                    {
                        return Task.CompletedTask;
                    });

            var versionedMessage = new VersionedMessage
            {
                Data = new TestEntityOne().SerializeToString(SerializerType.Json),
                Version = typeof(TestEntityOne).GetVersion(),
            };

            // Act
            var stopwatch = Stopwatch.StartNew();
            Assert.ThrowsAsync<DivideByZeroException>(() => versionedMessageHandler.PostAsync(versionedMessage));
            stopwatch.Stop();

            // Assert
            this.WriteTimeElapsed(stopwatch);
        }

        /// <summary>
        /// Versioned the message handler when no match expect default.
        /// </summary>
        /// <returns>The <see cref="Task"/>.</returns>
        [Test]
        public async Task VersionedMessageHandler_WhenNoMatch_ExpectDefault()
        {
            // Arrange
            var entityOne = default(TestEntityOne);
            var entityTwo = default(TestEntityTwo);
            var defaultValue = default(string);
            var exception = default(Exception);

            var versionedMessageHandler = new AsyncVersionedMessageHandler(SerializerType.Json, true)
                .On<TestEntityOne>(
                    e =>
                    {
                        entityOne = e;
                        return Task.CompletedTask;
                    })
                .On<TestEntityTwo>(
                    e =>
                    {
                        entityTwo = e;
                        return Task.CompletedTask;
                    })
                .Else(
                    s =>
                    {
                        defaultValue = s;
                        return Task.CompletedTask;
                    })
                .OnError(
                    e =>
                    {
                        exception = e;
                        return Task.CompletedTask;
                    });

            var versionedMessage = new VersionedMessage
            {
                Data = new TestEntityThree { Value1 = 25.3m }.SerializeToString(SerializerType.Json),
                Version = typeof(TestEntityThree).GetVersion(),
            };

            // Act
            var stopwatch = Stopwatch.StartNew();
            await versionedMessageHandler.PostAsync(versionedMessage).ConfigureAwait(false);
            stopwatch.Stop();

            // Assert
            this.WriteTimeElapsed(stopwatch);

            Assert.That(entityOne, Is.Null);
            Assert.That(entityTwo, Is.Null);
            Assert.That(defaultValue, Is.Not.Null);
            Assert.That(exception, Is.Null);

            Assert.That(defaultValue, Is.EqualTo("{\"Value1\":25.3}"));
        }

        /// <summary>
        /// Versioned message handler when version match expect action.
        /// </summary>
        /// <returns>The <see cref="Task"/>.</returns>
        [Test]
        public async Task VersionedMessageHandler_WhenVersionMatchOnFirst_ExpectAction()
        {
            // Arrange
            var entityOne = default(TestEntityOne);
            var entityTwo = default(TestEntityTwo);
            var defaultValue = default(string);
            var exception = default(Exception);

            var versionedMessageHandler = new AsyncVersionedMessageHandler(SerializerType.ProtocolBuffers, true)
                .On<TestEntityOne>(
                    e =>
                    {
                        entityOne = e;
                        return Task.CompletedTask;
                    })
                .On<TestEntityTwo>(
                    e =>
                    {
                        entityTwo = e;
                        return Task.CompletedTask;
                    })
                .Else(
                    s =>
                    {
                        defaultValue = s;
                        return Task.CompletedTask;
                    })
                .OnError(
                    e =>
                    {
                        exception = e;
                        return Task.CompletedTask;
                    });

            var versionedMessage = new VersionedMessage
            {
                Data = new TestEntityOne { Value1 = "test", Value2 = 42 }.SerializeToString(),
                Version = typeof(TestEntityOne).GetVersion(),
            };

            // Act
            var stopwatch = Stopwatch.StartNew();
            await versionedMessageHandler.PostAsync(versionedMessage).ConfigureAwait(false);
            stopwatch.Stop();

            // Assert
            this.WriteTimeElapsed(stopwatch);

            Assert.That(entityOne, Is.Not.Null);
            Assert.That(entityTwo, Is.Null);
            Assert.That(defaultValue, Is.Null);
            Assert.That(exception, Is.Null);

            Assert.That(entityOne.Value1, Is.EqualTo("test"));
            Assert.That(entityOne.Value2, Is.EqualTo(42));
        }

        /// <summary>
        /// Versioned message handler when version match expect action.
        /// </summary>
        /// <returns>The <see cref="Task"/>.</returns>
        [Test]
        public async Task VersionedMessageHandler_WhenVersionMatchOnSecond_ExpectAction()
        {
            // Arrange
            var entityOne = default(TestEntityOne);
            var entityTwo = default(TestEntityTwo);
            var defaultValue = default(string);
            var exception = default(Exception);

            var versionedMessageHandler = new AsyncVersionedMessageHandler(SerializerType.ProtocolBuffers, true)
                .On<TestEntityOne>(
                    e =>
                    {
                        entityOne = e;
                        return Task.CompletedTask;
                    })
                .On<TestEntityTwo>(
                    typeof(TestEntityTwo).GetVersion(),
                    e =>
                    {
                        entityTwo = e;
                        return Task.CompletedTask;
                    })
                .Else(
                    s =>
                    {
                        defaultValue = s;
                        return Task.CompletedTask;
                    })
                .OnError(
                    e =>
                    {
                        exception = e;
                        return Task.CompletedTask;
                    });

            var versionedMessage = new VersionedMessage
            {
                Data = new TestEntityTwo { Value1 = "test", Value2 = new DateTime(2018, 1, 1) }.SerializeToString(),
                Version = typeof(TestEntityTwo).GetVersion(),
            };

            // Act
            var stopwatch = Stopwatch.StartNew();
            await versionedMessageHandler.PostAsync(versionedMessage).ConfigureAwait(false);
            stopwatch.Stop();

            // Assert
            this.WriteTimeElapsed(stopwatch);

            Assert.That(entityOne, Is.Null);
            Assert.That(entityTwo, Is.Not.Null);
            Assert.That(defaultValue, Is.Null);
            Assert.That(exception, Is.Null);

            Assert.That(entityTwo.Value1, Is.EqualTo("test"));
            Assert.That(entityTwo.Value2, Is.EqualTo(new DateTime(2018, 1, 1)));
        }
    }
}