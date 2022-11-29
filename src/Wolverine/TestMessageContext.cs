using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wolverine.Runtime.ResponseReply;
using Wolverine.Transports.Local;

namespace Wolverine;

/// <summary>
/// Stand in "spy" for IMessageContext/IMessagePublisher to facilitate unit testing
/// in applications using Wolverine
/// </summary>
public class TestMessageContext : IMessageContext
{
    public TestMessageContext(object message)
    {
        Envelope = new Envelope(message);
        CorrelationId = Guid.NewGuid().ToString();
    }

    public TestMessageContext() : this(new object())
    {
    }

    public string? CorrelationId { get; set; }
    public Envelope? Envelope { get; }


    private readonly List<object> _invoked = new();
    
    /// <summary>
    /// Messages that were executed inline from this context
    /// </summary>
    public IReadOnlyList<object> Invoked => _invoked;

    Task ICommandBus.InvokeAsync(object message, CancellationToken cancellation)
    {
        _invoked.Add(message);
        return Task.CompletedTask;
    }

    Task<T?> ICommandBus.InvokeAsync<T>(object message, CancellationToken cancellation) where T : default
    {
        throw new NotSupportedException("This function is not yet supported within the TestMessageContext");
    }

    private readonly List<object> _enqueued = new();
    
    /// <summary>
    /// All messages that were enqueued or scheduled locally through this context
    /// </summary>
    public IReadOnlyList<object> Enqueued => _enqueued;
    
    ValueTask ICommandBus.EnqueueAsync<T>(T message)
    {
        _enqueued.Add(message);
        return new ValueTask();
    }

    ValueTask ICommandBus.EnqueueAsync<T>(T message, string workerQueueName)
    {
        var uri = new LocalQueueSettings(workerQueueName).Uri;
        var envelope = new Envelope { Message = message, Destination = uri };
        _enqueued.Add(envelope);

        return new ValueTask();
    }

    private readonly List<object> _published = new();
    private readonly List<object> _sent = new();

    /// <summary>
    /// All messages "published" through this context. If in doubt use AllOutgoing instead.
    /// </summary>
    public IReadOnlyList<object> Published => _published;
    
    /// <summary>
    /// All messages "sent" through this context with the SendAsync() semantics that require. If in doubt use AllOutgoing instead.
    /// a subscriber
    /// </summary>
    public IReadOnlyList<object> Sent => _sent;

    /// <summary>
    /// All outgoing messages sent or published or scheduled through this context
    /// </summary>
    public IReadOnlyList<object> AllOutgoing => _published.Concat(_sent).Concat(_responses).ToArray();

    /// <summary>
    /// 
    /// </summary>
    public IReadOnlyList<Envelope> LocallyScheduledMessages =>
        Enqueued
            .OfType<Envelope>()
            .Where(x => x.Status == EnvelopeStatus.Scheduled)
            .ToArray();

    Task<Guid> ICommandBus.ScheduleAsync<T>(T message, DateTimeOffset executionTime)
    {
        var envelope = new Envelope
        {
            Message = message, ScheduledTime = executionTime,
            Status = EnvelopeStatus.Scheduled
        };
        
        _enqueued.Add(envelope);

        return Task.FromResult(envelope.Id);
    }

    Task<Guid> ICommandBus.ScheduleAsync<T>(T message, TimeSpan delay)
    {
        var envelope = new Envelope
        {
            Message = message, 
            ScheduleDelay = delay, 
            Status = EnvelopeStatus.Scheduled
        };
        
        _enqueued.Add(envelope);

        return Task.FromResult(envelope.Id);
    }

    ValueTask IMessagePublisher.SendAsync<T>(T message, DeliveryOptions? options)
    {
        var envelope = new Envelope { Message = message };
        options?.Override(envelope);
        
        _sent.Add(envelope);

        return new ValueTask();
    }

    ValueTask IMessagePublisher.PublishAsync<T>(T message, DeliveryOptions? options)
    {
        var envelope = new Envelope { Message = message };
        options?.Override(envelope);
        
        _published.Add(envelope);

        return new ValueTask();
    }

    ValueTask IMessagePublisher.SendToTopicAsync(string topicName, object message, DeliveryOptions? options)
    {
        var envelope = new Envelope { Message = message, TopicName = topicName};
        options?.Override(envelope);
        
        _published.Add(envelope);

        return new ValueTask();
    }

    ValueTask IMessagePublisher.SendToEndpointAsync(string endpointName, object message, DeliveryOptions? options)
    {
        var envelope = new Envelope { Message = message, EndpointName = endpointName };
        options?.Override(envelope);
        
        _published.Add(envelope);

        return new ValueTask();
    }

    ValueTask IMessagePublisher.SendAsync<T>(Uri destination, T message, DeliveryOptions? options)
    {
        var envelope = new Envelope { Message = message, Destination = destination};
        options?.Override(envelope);
        
        _published.Add(envelope);

        return new ValueTask();
    }

    /// <summary>
    /// All scheduled outgoing (to external message transports) messages
    /// </summary>
    /// <returns></returns>
    public IReadOnlyList<Envelope> ScheduledOutgoingMessages() => AllOutgoing
        .OfType<Envelope>()
        .Where(x => x.Status == EnvelopeStatus.Scheduled)
        .ToArray();

    ValueTask IMessagePublisher.SchedulePublishAsync<T>(T message, DateTimeOffset time, DeliveryOptions? options)
    {
        var envelope = new Envelope
        {
            Message = message, ScheduledTime = time,
            Status = EnvelopeStatus.Scheduled
        };
        
        options?.Override(envelope);
        _published.Add(envelope);

        return new ValueTask();
    }

    ValueTask IMessagePublisher.SchedulePublishAsync<T>(T message, TimeSpan delay, DeliveryOptions? options)
    {
        var envelope = new Envelope { Message = message, ScheduleDelay = delay, Status = EnvelopeStatus.Scheduled};
        options?.Override(envelope);
        _published.Add(envelope);

        return new ValueTask();
    }

    Task<Acknowledgement> IMessagePublisher.SendAndWaitAsync(object message, CancellationToken cancellation, TimeSpan? timeout)
    {
        _sent.Add(message);
        return Task.FromResult(new Acknowledgement());
    }

    Task<Acknowledgement> IMessagePublisher.SendAndWaitAsync(Uri destination, object message, CancellationToken cancellation,
        TimeSpan? timeout)
    {
        var envelope = new Envelope { Message = message, Destination = destination };
        _sent.Add(envelope);
        return Task.FromResult(new Acknowledgement());
    }

    Task<Acknowledgement> IMessagePublisher.SendAndWaitAsync(string endpointName, object message, CancellationToken cancellation,
        TimeSpan? timeout)
    {
        var envelope = new Envelope { Message = message, EndpointName = endpointName};
        _sent.Add(envelope);
        return Task.FromResult(new Acknowledgement());
    }

    Task<T> IMessagePublisher.RequestAsync<T>(object message, CancellationToken cancellation, TimeSpan? timeout)
    {
        throw new NotSupportedException();
    }

    Task<T> IMessagePublisher.RequestAsync<T>(Uri destination, object message, CancellationToken cancellation,
        TimeSpan? timeout)
    {
        throw new NotSupportedException();
    }

    Task<T> IMessagePublisher.RequestAsync<T>(string endpointName, object message, CancellationToken cancellation,
        TimeSpan? timeout)
    {
        throw new NotSupportedException();
    }

    private readonly List<object> _responses = new();

    /// <summary>
    /// Messages that were specifically sent back to the original sender of the
    /// current message
    /// </summary>
    public IReadOnlyList<object> ResponsesToSender => _responses;

    ValueTask IMessageContext.RespondToSenderAsync(object response)
    {
        _responses.Add(response);

        return new ValueTask();
    }
}