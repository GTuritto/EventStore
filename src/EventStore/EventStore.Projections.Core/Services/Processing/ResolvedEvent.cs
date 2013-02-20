﻿// Copyright (c) 2012, Event Store LLP
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
// 
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 

using System;

namespace EventStore.Projections.Core.Services.Processing
{
    public class ResolvedEvent
    {
        public static ResolvedEvent Create(Guid eventId, string eventType, bool isJson, byte[] data, byte[] metadata, DateTime timestamp)
        {
            return new ResolvedEvent(eventId, eventType, isJson, data, metadata, timestamp);
        }

        public static ResolvedEvent Sample(Guid eventId, string eventType, bool isJson, byte[] data, byte[] metadata, DateTime? timestamp = null)
        {
            return new ResolvedEvent(eventId, eventType, isJson, data, metadata, timestamp ?? DateTime.UtcNow);
        }

        public readonly Guid EventId;
        public readonly string EventType;
        public readonly bool IsJson;
        public readonly DateTime Timestamp;

        public readonly byte[] Data;
        public readonly byte[] Metadata;

        private ResolvedEvent(Guid eventId, string eventType, bool isJson, byte[] data, byte[] metadata, DateTime timestamp)
        {
            if (Guid.Empty == eventId)
                throw new ArgumentException("Empty eventId provided.");
            if (string.IsNullOrEmpty(eventType))
                throw new ArgumentException("Empty eventType provided.");

            EventId = eventId;
            EventType = eventType;
            IsJson = isJson;
            Timestamp = timestamp;

            Data = data ?? Common.Utils.Empty.ByteArray;
            Metadata = metadata ?? Common.Utils.Empty.ByteArray;
        }
    }
}
