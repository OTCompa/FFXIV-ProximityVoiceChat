﻿using SIPSorcery.Net;

namespace ProximityVoiceChat.WebRTC
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public struct SignalMessage
    {
        public struct SignalPayload
        {
            public struct Connection
            {
                public string socketId;
                public string peerId;
                public string peerType;
                public ushort audioState;
            }

            public struct IcePayload
            {
                public string candidate;
                public string sdpMid;
                public int sdpMLineIndex;
                public string foundation;
                public RTCIceComponent component;
                public uint priority;
                public string address;
                public RTCIceProtocol protocol;
                public ushort port;
                public RTCIceCandidateType type;
                public RTCIceTcpCandidateType? tcpType;
                public string? relatedAddress;
                public ushort? relatedPort;
                public string usernameFragment;
            }

            public struct TURNConfig
            {
                public string url;
                public string username;
                public string password;
            }

            public string action;
            public Connection[] connections;
            public bool? bePolite;
            public TURNConfig? turnConfig;
            public RTCSessionDescriptionInit sdp;
            public IcePayload? ice;
        }

        public string from;
        public string target;
        public SignalPayload payload;
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
}
