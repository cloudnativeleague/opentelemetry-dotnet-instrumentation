// <copyright file="ActivityHelperTests.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System.Diagnostics;
using FluentAssertions;
using FluentAssertions.Execution;
using NSubstitute;
using OpenTelemetry.AutoInstrumentation.Tagging;
using OpenTelemetry.AutoInstrumentation.Util;
using Xunit;

namespace OpenTelemetry.AutoInstrumentation.Tests.Util;

public class ActivityHelperTests
{
    [Fact]
    public void SetException_NotThrow_WhenActivityIsNull()
    {
        const Activity? activity = null;

        var action = () => activity.SetException(new Exception());

        action.Should().NotThrow();
    }

    [Fact]
    public void SetException_NotThrow_WhenExceptionIsNull()
    {
        var activity = new Activity("test-operation");

        var action = () =>
        {
            activity.SetException(null);
            activity.Dispose();
        };

        action.Should().NotThrow();
    }

    [Fact]
    public void SetException_SetsExceptionDetails()
    {
        using var activity = new Activity("test-operation");

        var exceptionMessage = "test-message";
        activity.SetException(new Exception(exceptionMessage));

        using (new AssertionScope())
        {
            activity.Tags.First(x => x.Key == "otel.status_code").Value.Should().Be("ERROR");
            activity.Tags.First(x => x.Key == "otel.status_description").Value.Should().Be(exceptionMessage);
            activity.Events.Should().HaveCount(1);
        }
    }

    [Fact]
    public void StartActivityWithTags_ReturnsNull_WhenActivitySourceIsNull()
    {
        const ActivitySource? activitySource = null;

        using var activity = activitySource.StartActivityWithTags("test-operation", ActivityKind.Internal, Substitute.For<ITags>());

        activity.Should().BeNull();
    }

    [Fact]
    public void StartActivityWithTags_ReturnsNull_WhenActivitySourceDoesNotHaveListener()
    {
        using var activitySource = new ActivitySource("test-source");

        using var activity = activitySource.StartActivityWithTags("test-operation", ActivityKind.Internal, Substitute.For<ITags>());

        using (new AssertionScope())
        {
            activitySource.HasListeners().Should().BeFalse();
            activity.Should().BeNull();
        }
    }

    [Theory]
    [InlineData(ActivityKind.Internal)]
    [InlineData(ActivityKind.Server)]
    [InlineData(ActivityKind.Client)]
    [InlineData(ActivityKind.Producer)]
    [InlineData(ActivityKind.Consumer)]
    public void StartActivityWithTags_ReturnsActivity_WhenThereIsActivityListener(ActivityKind kind)
    {
        var tagsMock = Substitute.For<ITags>();
        tagsMock.GetAllTags().Returns(new List<KeyValuePair<string, string>>());

        using var activitySource = new ActivitySource("test-source");

        using var listener = CreateActivityListener(activitySource);

        using var activity = activitySource.StartActivityWithTags("test-operation", kind, tagsMock);

        using (new AssertionScope())
        {
            activitySource.HasListeners().Should().BeTrue();
            activity.Should().NotBeNull();
            activity?.Kind.Should().Be(kind);
        }
    }

    [Fact]
    public void StartActivityWithTags_SetsCorrectTags()
    {
        var tags = new List<KeyValuePair<string, string>>
        {
            new("key1", "value1"),
            new("key2", "value2")
        };

        var tagsMock = Substitute.For<ITags>();
        tagsMock.GetAllTags().Returns(tags);

        using var activitySource = new ActivitySource("test-source");

        using var listener = CreateActivityListener(activitySource);

        using var activity = activitySource.StartActivityWithTags("test-operation", ActivityKind.Internal, tagsMock);

        tagsMock.GetAllTags().Returns(tags);

        using (new AssertionScope())
        {
            activitySource.HasListeners().Should().BeTrue();
            activity.Should().NotBeNull();
            activity?.Tags.Should().BeEquivalentTo(tags);
        }
    }

    private static ActivityListener CreateActivityListener(ActivitySource activitySource)
    {
        var listener = new ActivityListener();
        listener.ShouldListenTo = source => source == activitySource;
        listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData;
        ActivitySource.AddActivityListener(listener);

        return listener;
    }
}
