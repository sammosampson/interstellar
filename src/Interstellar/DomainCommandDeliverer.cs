﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Interstellar
{
    public class DomainCommandDeliverer
    {
        private readonly AggregateFactory aggregateFactory;
        private readonly EventStreamLoader eventStreamLoader;
        private readonly IEventStore eventStore;
        private readonly AggregateLookup aggregateLookup;

        public DomainCommandDeliverer(
            AggregateFactory aggregateFactory,
            EventStreamLoader eventStreamLoader,
            IEventStore eventStore, 
            AggregateLookup aggregateLookup)
        {
            this.aggregateFactory = aggregateFactory;
            this.eventStreamLoader = eventStreamLoader;
            this.eventStore = eventStore;
            this.aggregateLookup = aggregateLookup;
        }

        public Task DeliverCommandAsync<TCommand>(TCommand command, IDictionary<string, object> headers)
        {
            MessageContext.Current.Headers = headers;
            return DeliverCommandAsync(command, 0);
        }

        private async Task DeliverCommandAsync<TCommand>(TCommand command, int attempts)
        {
            var aggregateResolution = ResolveAggregate(command);

            using var uow = new UnitOfWork(aggregateResolution.Aggregate.StreamId);
            IEnumerable<EventPayload> events = await eventStreamLoader.LoadEventsAsync(aggregateResolution.StreamIds);
            aggregateResolution.Aggregate.ReplayEvents(events);
            await aggregateResolution.Aggregate.ReceiveCommandAsync(command);
            await StoreEvents(command, attempts, uow);
        }

        private AggregateResolution ResolveAggregate<TCommand>(TCommand command)
        {
            return aggregateLookup.ResolveToAggregate(command, aggregateFactory);
        }

        private async Task StoreEvents<TCommand>(TCommand command, int attempts, UnitOfWork uow)
        {
            try
            {
                await eventStore.StoreEventsAsync(uow.EventsAdded);
            }
            catch (ExpectedEventIndexIncorrectException)
            {
                if (attempts == 3)
                {
                    throw;
                }

                await DeliverCommandAsync(command, ++attempts);
            }
        }
    }
}