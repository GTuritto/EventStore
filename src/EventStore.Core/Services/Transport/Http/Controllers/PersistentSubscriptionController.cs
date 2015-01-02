using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Transport.Http;
using EventStore.Transport.Http.Codecs;
using EventStore.Transport.Http.EntityManagement;

namespace EventStore.Core.Services.Transport.Http.Controllers
{
    public class PersistentSubscriptionController : CommunicationController
    {
        private readonly IHttpForwarder _httpForwarder;
        private readonly IPublisher _networkSendQueue;
        private static readonly ICodec[] DefaultCodecs = {Codec.Json, Codec.Xml};
        private static readonly ILogger Log = LogManager.GetLoggerFor<PersistentSubscriptionController>();

        public PersistentSubscriptionController(IHttpForwarder httpForwarder, IPublisher publisher, IPublisher networkSendQueue)
            : base(publisher)
        {
            _httpForwarder = httpForwarder;
            _networkSendQueue = networkSendQueue;
        }

        protected override void SubscribeCore(IHttpService service)
        {
            Register(service, "/subscriptions/{stream}/{subscription}", HttpMethod.Get, GetSubscriptionInfo, Codec.NoCodecs, DefaultCodecs);
            Register(service, "/subscriptions/{stream}", HttpMethod.Get, GetSubscriptionInfoForStream, Codec.NoCodecs, DefaultCodecs);
            Register(service, "/subscriptions", HttpMethod.Get, GetAllSubscriptionInfo, Codec.NoCodecs, DefaultCodecs);
            Register(service, "/subscriptions/{stream}/{subscription}", HttpMethod.Put, PutSubscription, DefaultCodecs, DefaultCodecs);
            Register(service, "/subscriptions/{stream}/{subscription}", HttpMethod.Post, PostSubscription, DefaultCodecs, DefaultCodecs);
            RegisterUrlBased(service, "/subscriptions/{stream}/{subscription}", HttpMethod.Delete, DeleteSubscription);
            RegisterUrlBased(service, "/subscriptions/{stream}/{subscription}/replayParked", HttpMethod.Post, ReplayParkedMessages);
            Register(service, "/subscriptions/{stream}/{subscription}/messages?count={count}", HttpMethod.Get, GetNextNMessages, Codec.NoCodecs, DefaultCodecs);
            RegisterUrlBased(service, "/subscriptions/{stream}/{subscription}/ack?id={messageid}", HttpMethod.Post, AckMessage);
            RegisterUrlBased(service, "/subscriptions/{stream}/{subscription}/nack?id={messageid}", HttpMethod.Post, NackMessage);
        }

        private void AckMessage(HttpEntityManager http, UriTemplateMatch match)
        {
            http.ReplyStatus(HttpStatusCode.ServiceUnavailable, "This service has not been implemented yet.", exception => { });
        }
        private void NackMessage(HttpEntityManager http, UriTemplateMatch match)
        {
            http.ReplyStatus(HttpStatusCode.ServiceUnavailable, "This service has not been implemented yet.", exception => { });
        }


        private void GetNextNMessages(HttpEntityManager http, UriTemplateMatch match)
        {
           //called on GET of messages
           //trial implementation 
            if (_httpForwarder.ForwardRequest(http))
                return;
            var envelope = new SendToHttpEnvelope(
                _networkSendQueue, http,
                (args, message) => http.ResponseCodec.To(message),
                (args, message) =>
                {
                    int code;
                    var m = message as ClientMessage.ReadNextNPersistentMessagesCompleted;
                    if (m == null) throw new Exception("unexpected message " + message);
                    switch (m.Result)
                    {
                        case ClientMessage.ReadNextNPersistentMessagesCompleted.ReadNextNPersistentMessagesResult.Success:
                            code = HttpStatusCode.OK;
                            break;
                        case ClientMessage.ReadNextNPersistentMessagesCompleted.ReadNextNPersistentMessagesResult.DoesNotExist:
                            code = HttpStatusCode.NotFound;
                            break;
                        case ClientMessage.ReadNextNPersistentMessagesCompleted.ReadNextNPersistentMessagesResult.AccessDenied:
                            code = HttpStatusCode.Unauthorized;
                            break;
                        default:
                            code = HttpStatusCode.InternalServerError;
                            break;
                    }

                    return new ResponseConfiguration(code, http.ResponseCodec.ContentType,
                        http.ResponseCodec.Encoding);
                });
            var groupname = match.BoundVariables["subscription"];
            var stream = match.BoundVariables["stream"];
            var count = 1; //default
            if (match.QueryParameters.HasKeys() &&
                match.QueryParameters.AllKeys.Contains("count")
                )
            {
                var toParse = match.QueryParameters["count"];
                if (!int.TryParse(toParse, out count) ||
                    count > 100 ||
                    count < 1)
                {
                    SendBadRequest(
                        http,
                        string.Format(
                            "Message count must be an integer between 1 and 100 'count' ='{0}'", 
                            toParse));
                    return;
                }
            }
            var cmd = new ClientMessage.ReadNextNPersistentMessages(
                                             Guid.NewGuid(),
                                             Guid.NewGuid(),
                                             envelope, 
                                             stream, 
                                             groupname,
                                             count,
                                             http.User);
            Publish(cmd);
        }


        private void ReplayParkedMessages(HttpEntityManager http, UriTemplateMatch match)
        {
            if (_httpForwarder.ForwardRequest(http))
                return;
            var envelope = new SendToHttpEnvelope(
                _networkSendQueue, http,
                (args, message) => http.ResponseCodec.To(message),
                (args, message) =>
                {
                    int code;
                    var m = message as ClientMessage.ReplayMessagesReceived;
                    if (m == null) throw new Exception("unexpected message " + message);
                    switch (m.Result)
                    {
                        case ClientMessage.ReplayMessagesReceived.ReplayMessagesReceivedResult.Success:
                            code = HttpStatusCode.OK;
                            break;
                        case ClientMessage.ReplayMessagesReceived.ReplayMessagesReceivedResult.DoesNotExist:
                            code = HttpStatusCode.NotFound;
                            break;
                        case ClientMessage.ReplayMessagesReceived.ReplayMessagesReceivedResult.AccessDenied:
                            code = HttpStatusCode.Unauthorized;
                            break;
                        default:
                            code = HttpStatusCode.InternalServerError;
                            break;
                    }

                    return new ResponseConfiguration(code, http.ResponseCodec.ContentType,
                        http.ResponseCodec.Encoding);
                });
            var groupname = match.BoundVariables["subscription"];
            var stream = match.BoundVariables["stream"];
            var cmd = new ClientMessage.ReplayAllParkedMessages(Guid.NewGuid(), Guid.NewGuid(), envelope, stream, groupname, http.User);
            Publish(cmd);
        }

        private void PutSubscription(HttpEntityManager http, UriTemplateMatch match)
        {
            if (_httpForwarder.ForwardRequest(http))
                return;
            var groupname = match.BoundVariables["subscription"];
            var stream = match.BoundVariables["stream"];
            var envelope = new SendToHttpEnvelope(
                _networkSendQueue, http,
                (args, message) => http.ResponseCodec.To(message),
                (args,message) =>
                {
                    int code;
                    var m = message as ClientMessage.CreatePersistentSubscriptionCompleted;
                    if(m==null) throw new Exception("unexpected message " + message);
                    switch (m.Result)
                    {
                        case ClientMessage.CreatePersistentSubscriptionCompleted.CreatePersistentSubscriptionResult.Success:
                            code = HttpStatusCode.Created;
                            //TODO competing return uri to subscription
                            break;
                        case ClientMessage.CreatePersistentSubscriptionCompleted.CreatePersistentSubscriptionResult.AlreadyExists:
                            code = HttpStatusCode.Conflict;
                            break;
                        case ClientMessage.CreatePersistentSubscriptionCompleted.CreatePersistentSubscriptionResult.AccessDenied:
                            code = HttpStatusCode.Unauthorized;
                            break;
                        default:
                            code = HttpStatusCode.InternalServerError;
                            break;
                    }
                    return new ResponseConfiguration(code, http.ResponseCodec.ContentType,
                        http.ResponseCodec.Encoding, new KeyValuePair<string, string>("location", MakeUrl(http, "/subscriptions/" + stream + "/" + groupname)));
                });
            http.ReadTextRequestAsync(
                (o, s) =>
                {
                    var data = http.RequestCodec.From<SubscriptionConfigData>(s);
                    //TODO competing validate data?
                    var message = new ClientMessage.CreatePersistentSubscription(Guid.NewGuid(), 
                        Guid.NewGuid(),
                        envelope, 
                        stream, 
                        groupname, 
                        data == null || data.ResolveLinktos, 
                        data == null ? 0 : data.StartFrom, 
                        data == null ? 0 : data.MessageTimeoutMilliseconds, 
                        data == null || data.ExtraStatistics,
                        data == null ? 10 : data.MaxRetryCount,
                        data == null ? 500 : data.BufferSize,
                        data == null ? 500 : data.LiveBufferSize,
                        data == null ? 20 : data.ReadBatchSize,
                        data == null || data.PreferRoundRobin, 
                        data == null ? 1000 : data.CheckPointAfterMilliseconds,
                        data == null ? 10 : data.MinCheckPointCount,
                        data == null ? 500 : data.MaxCheckPointCount,
                        http.User,
                        "",
                        "");
                    Publish(message);
                }, x => Log.DebugException(x, "Reply Text Content Failed."));
        }


        private void PostSubscription(HttpEntityManager http, UriTemplateMatch match)
        {
            if (_httpForwarder.ForwardRequest(http))
                return;
            var groupname = match.BoundVariables["subscription"];
            var stream = match.BoundVariables["stream"];
            var envelope = new SendToHttpEnvelope(
                _networkSendQueue, http,
                (args, message) => http.ResponseCodec.To(message),
                (args, message) =>
                {
                    int code;
                    var m = message as ClientMessage.UpdatePersistentSubscriptionCompleted;
                    if (m == null) throw new Exception("unexpected message " + message);
                    switch (m.Result)
                    {
                        case ClientMessage.UpdatePersistentSubscriptionCompleted.UpdatePersistentSubscriptionResult.Success:
                            code = HttpStatusCode.OK;
                            //TODO competing return uri to subscription
                            break;
                        case ClientMessage.UpdatePersistentSubscriptionCompleted.UpdatePersistentSubscriptionResult.DoesNotExist:
                            code = HttpStatusCode.NotFound;
                            break;
                        case ClientMessage.UpdatePersistentSubscriptionCompleted.UpdatePersistentSubscriptionResult.AccessDenied:
                            code = HttpStatusCode.Unauthorized;
                            break;
                        default:
                            code = HttpStatusCode.InternalServerError;
                            break;
                    }
                    return new ResponseConfiguration(code, http.ResponseCodec.ContentType,
                        http.ResponseCodec.Encoding, new KeyValuePair<string, string>("location", MakeUrl(http, "/subscriptions/" + stream + "/" + groupname)));
                });
            http.ReadTextRequestAsync(
                (o, s) =>
                {
                    var data = http.RequestCodec.From<SubscriptionConfigData>(s);
                    //TODO competing validate data?
                    var message = new ClientMessage.UpdatePersistentSubscription(Guid.NewGuid(),
                        Guid.NewGuid(),
                        envelope,
                        stream,
                        groupname,
                        data == null || data.ResolveLinktos,
                        data == null ? 0 : data.StartFrom,
                        data == null ? 0 : data.MessageTimeoutMilliseconds,
                        data == null || data.ExtraStatistics,
                        data == null ? 10 : data.MaxRetryCount,
                        data == null ? 500 : data.BufferSize,
                        data == null ? 500 : data.LiveBufferSize,
                        data == null ? 20 : data.ReadBatchSize,
                        data == null || data.PreferRoundRobin,
                        data == null ? 1000 : data.CheckPointAfterMilliseconds,
                        data == null ? 10 : data.MinCheckPointCount,
                        data == null ? 500 : data.MaxCheckPointCount,
                        http.User,
                        "",
                        "");
                    Publish(message);
                }, x => Log.DebugException(x, "Reply Text Content Failed."));
        }


        private void DeleteSubscription(HttpEntityManager http, UriTemplateMatch match)
        {
            if (_httpForwarder.ForwardRequest(http))
                return;
            var envelope = new SendToHttpEnvelope(
                _networkSendQueue, http,
                (args, message) => http.ResponseCodec.To(message),
                (args, message) =>
                {
                    int code;
                    var m = message as ClientMessage.DeletePersistentSubscriptionCompleted;
                    if (m == null) throw new Exception("unexpected message " + message);
                    switch (m.Result)
                    {
                        case ClientMessage.DeletePersistentSubscriptionCompleted.DeletePersistentSubscriptionResult.Success:
                            code = HttpStatusCode.OK;
                            break;
                        case ClientMessage.DeletePersistentSubscriptionCompleted.DeletePersistentSubscriptionResult.DoesNotExist:
                            code = HttpStatusCode.NotFound;
                            break;
                        case ClientMessage.DeletePersistentSubscriptionCompleted.DeletePersistentSubscriptionResult.AccessDenied:
                            code = HttpStatusCode.Unauthorized;
                            break;
                        default:
                            code = HttpStatusCode.InternalServerError;
                            break;
                    }

                    return new ResponseConfiguration(code, http.ResponseCodec.ContentType,
                        http.ResponseCodec.Encoding);
                });
            var groupname = match.BoundVariables["subscription"];
            var stream = match.BoundVariables["stream"];
            var cmd = new ClientMessage.DeletePersistentSubscription(Guid.NewGuid(), Guid.NewGuid(),envelope, stream, groupname, http.User);
            Publish(cmd);
        }

        private void GetAllSubscriptionInfo(HttpEntityManager http, UriTemplateMatch match)
        {
            if (_httpForwarder.ForwardRequest(http))
                return;
            var envelope = new SendToHttpEnvelope(
                _networkSendQueue, http,
                (args, message) => http.ResponseCodec.To(ToSummaryDto(http, message as MonitoringMessage.GetPersistentSubscriptionStatsCompleted)),
                (args, message) => StatsConfiguration(http, message));
            var cmd = new MonitoringMessage.GetAllPersistentSubscriptionStats(envelope);
            Publish(cmd);
        }

        private void GetSubscriptionInfoForStream(HttpEntityManager http, UriTemplateMatch match)
        {
            if (_httpForwarder.ForwardRequest(http))
                return;
            var stream = match.BoundVariables["stream"];
            var envelope = new SendToHttpEnvelope(
                _networkSendQueue, http,
                (args, message) => http.ResponseCodec.To(ToSummaryDto(http, message as MonitoringMessage.GetPersistentSubscriptionStatsCompleted)),
                (args, message) => StatsConfiguration(http, message));
            var cmd = new MonitoringMessage.GetStreamPersistentSubscriptionStats(envelope, stream);
            Publish(cmd);
        }

        private void GetSubscriptionInfo(HttpEntityManager http, UriTemplateMatch match)
        {
            if (_httpForwarder.ForwardRequest(http))
                return;
            var stream = match.BoundVariables["stream"];
            var groupName = match.BoundVariables["subscription"];
            var envelope = new SendToHttpEnvelope(
                _networkSendQueue, http,
                (args, message) => http.ResponseCodec.To(ToDto(http, message as MonitoringMessage.GetPersistentSubscriptionStatsCompleted).FirstOrDefault()),
                (args, message) => StatsConfiguration(http, message));
            var cmd = new MonitoringMessage.GetPersistentSubscriptionStats(envelope, stream, groupName);
            Publish(cmd);
        }


        private static ResponseConfiguration StatsConfiguration(HttpEntityManager http, Message message)
        {
            int code;
            var m = message as MonitoringMessage.GetPersistentSubscriptionStatsCompleted;
            if (m == null) throw new Exception("unexpected message " + message);
            switch (m.Result)
            {
                case MonitoringMessage.GetPersistentSubscriptionStatsCompleted.OperationStatus.Success:
                    code = HttpStatusCode.OK;
                    break;
                case MonitoringMessage.GetPersistentSubscriptionStatsCompleted.OperationStatus.NotFound:
                    code = HttpStatusCode.NotFound;
                    break;
                case MonitoringMessage.GetPersistentSubscriptionStatsCompleted.OperationStatus.NotReady:
                    code = HttpStatusCode.ServiceUnavailable;
                    break;
                default:
                    code = HttpStatusCode.InternalServerError;
                    break;
            }

            return new ResponseConfiguration(code, http.ResponseCodec.ContentType,
                http.ResponseCodec.Encoding);
        }

        private IEnumerable<SubscriptionInfo> ToDto(HttpEntityManager manager, MonitoringMessage.GetPersistentSubscriptionStatsCompleted message)
        {
            if (message == null) yield break;
            if (message.SubscriptionStats == null) yield break;
            foreach (var stat in message.SubscriptionStats)
            {
                var info = new SubscriptionInfo
                {
                    Links = new List<RelLink>()
                    {
                        new RelLink(MakeUrl(manager, string.Format("/subscriptions/{0}/{1}", stat.EventStreamId,stat.GroupName)), "detail"),
                        new RelLink(MakeUrl(manager, string.Format("/subscriptions/{0}/{1}/replayParked", stat.EventStreamId,stat.GroupName)), "replayParked")
                    },
                    EventStreamId = stat.EventStreamId,
                    GroupName = stat.GroupName,
                    Status = stat.Status,
                    AverageItemsPerSecond = stat.AveragePerSecond,
                    TotalItemsProcessed = stat.TotalItems,
                    CountSinceLastMeasurement = stat.CountSinceLastMeasurement,
                    LastKnownEventNumber = stat.LastProcessedEventNumber,
                    LastProcessedEventNumber = stat.LastProcessedEventNumber,
                    ReadBufferCount = stat.ReadBufferCount,
                    LiveBufferCount = stat.LiveBufferCount,
                    RetryBufferCount = stat.RetryBufferCount,
                    ParkedMessageUri = MakeUrl(manager, string.Format("/streams/$persistentsubscription-{0}::{1}-parked", stat.EventStreamId, stat.GroupName)),
                    GetMessagesUri = MakeUrl(manager, string.Format("/streams/{0}/{1}/messages?c=10", stat.EventStreamId, stat.GroupName)),
                    Config = new SubscriptionConfigData()
                    {
                        CheckPointAfterMilliseconds = stat.CheckPointAfterMilliseconds,
                        BufferSize = stat.BufferSize,
                        LiveBufferSize = stat.LiveBufferSize,
                        MaxCheckPointCount = stat.MaxCheckPointCount,
                        MaxRetryCount = stat.MaxRetryCount,
                        MessageTimeoutMilliseconds = stat.MessageTimeoutMilliseconds,
                        MinCheckPointCount = stat.MinCheckPointCount,
                        PreferRoundRobin = stat.PreferRoundRobin,
                        ReadBatchSize = stat.ReadBatchSize,
                        ResolveLinktos = stat.ResolveLinktos,
                        StartFrom = stat.StartFrom,
                        ExtraStatistics = stat.ExtraStatistics,
                    },
                    Connections = new List<ConnectionInfo>()
                };
                if (stat.Connections != null)
                {
                    foreach (var connection in stat.Connections)
                    {
                        info.Connections.Add(new ConnectionInfo
                        {
                            Username = connection.Username, 
                            From = connection.From,
                            AverageItemsPerSecond = connection.AverageItemsPerSecond,
                            CountSinceLastMeasurement = connection.CountSinceLastMeasurement,
                            TotalItemsProcessed = connection.TotalItems,
                            AvailableSlots = connection.AvailableSlots,
                            ExtraStatistics = connection.ObservedMeasurements ?? new Dictionary<string, int>()
                        });
                    }
                }
                yield return info;
            }
        }

        private IEnumerable<SubscriptionSummary> ToSummaryDto(HttpEntityManager manager, MonitoringMessage.GetPersistentSubscriptionStatsCompleted message)
        {
            if (message == null) yield break;
            if (message.SubscriptionStats == null) yield break;
            foreach (var stat in message.SubscriptionStats)
            {
                var info = new SubscriptionSummary
                {
                    Links = new List<RelLink>()
                    {
                        new RelLink(MakeUrl(manager, string.Format("/subscriptions/{0}/{1}", stat.EventStreamId,stat.GroupName)), "detail"),
                    },
                    EventStreamId = stat.EventStreamId,
                    GroupName = stat.GroupName,
                    Status = stat.Status,
                    AverageItemsPerSecond = stat.AveragePerSecond,
                    TotalItemsProcessed = stat.TotalItems,
                    LastKnownEventNumber = stat.LastProcessedEventNumber,
                    LastProcessedEventNumber = stat.LastProcessedEventNumber,
                    ParkedMessageUri = MakeUrl(manager, string.Format("/streams/$persistentsubscription-{0}::{1}-parked", stat.EventStreamId, stat.GroupName)),
                    GetMessagesUri = MakeUrl(manager, string.Format("/streams/{0}/{1}/messages?c=10", stat.EventStreamId, stat.GroupName)),
                };
                if (stat.Connections != null)
                {
                    info.ConnectionCount = stat.Connections.Count;
                }
                yield return info;
            }
        }
        private class SubscriptionConfigData
        {
            public bool ResolveLinktos { get; set; }
            public int StartFrom { get; set; }
            public int MessageTimeoutMilliseconds { get; set; }
            public bool ExtraStatistics { get; set; }
            public int MaxRetryCount { get; set; }
            public int LiveBufferSize { get; set; }
            public int BufferSize { get; set; }
            public int ReadBatchSize { get; set; }
            public bool PreferRoundRobin { get; set; }
            public int CheckPointAfterMilliseconds { get; set; }
            public int MinCheckPointCount { get; set; }
            public int MaxCheckPointCount { get; set; }
        }

        private class SubscriptionSummary
        {
            public List<RelLink> Links { get; set; }
            public string EventStreamId { get; set; }
            public string GroupName { get; set; }
            public string ParkedMessageUri { get; set; }
            public string GetMessagesUri { get; set; }
            public string Status { get; set; }
            public decimal AverageItemsPerSecond { get; set; }
            public long TotalItemsProcessed { get; set; }
            public int LastProcessedEventNumber { get; set; }
            public int LastKnownEventNumber { get; set; }
            public int ConnectionCount { get; set; }
        }
        private class SubscriptionInfo
        {
            public List<RelLink> Links { get; set; }
            public string EventStreamId { get; set; }
            public string GroupName { get; set; }
            public string Status { get; set; }
            public decimal AverageItemsPerSecond { get; set; }
            public SubscriptionConfigData Config { get; set; }
            public string ParkedMessageUri { get; set; }
            public string GetMessagesUri { get; set; }
            public long TotalItemsProcessed { get; set; }
            public long CountSinceLastMeasurement { get; set; }
            public List<ConnectionInfo> Connections { get; set; }
            public int LastProcessedEventNumber { get; set; }
            public int LastKnownEventNumber { get; set; }
            public int ReadBufferCount { get; set; }
            public int LiveBufferCount { get; set; }
            public int RetryBufferCount { get; set; }
        }

        private class ConnectionInfo
        {
            public string From { get; set; }
            public string Username { get; set; }
            public decimal AverageItemsPerSecond { get; set; }
            public long TotalItemsProcessed { get; set; }
            public long CountSinceLastMeasurement { get; set; }
            public Dictionary<string, int> ExtraStatistics { get; set; }
            public int AvailableSlots { get; set; }
        }
    }
}