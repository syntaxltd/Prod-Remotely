﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MessagePack;
using Microsoft.MixedReality.WebRTC;
using Silgred.ScreenCast.Core.Services;
using Silgred.Shared.Enums;
using Silgred.Shared.Models;

namespace Silgred.ScreenCast.Core.Communication
{
    public class WebRtcSession : IDisposable
    {
        public ulong CurrentBuffer { get; private set; }

        public bool IsDataChannelOpen => CaptureChannel?.State == DataChannel.ChannelState.Open;
        public bool IsPeerConnected => PeerConnection?.IsConnected == true;
        private DataChannel CaptureChannel { get; set; }
        private PeerConnection PeerConnection { get; set; }

        public void Dispose()
        {
            CaptureChannel?.Dispose();
            PeerConnection?.Dispose();
        }

        public event EventHandler<(string candidate, int sdpMlineIndex, string sdpMid)> IceCandidateReady;

        public event EventHandler<string> LocalSdpReady;

        public void AddIceCandidate(string sdpMid, int sdpMlineIndex, string candidate)
        {
            PeerConnection.AddIceCandidate(sdpMid, sdpMlineIndex, candidate);
        }

        public async Task Init()
        {
            Logger.Debug("Starting WebRTC connection.");
            PeerConnection = new PeerConnection();

            var config = new PeerConnectionConfiguration
            {
                IceServers = new List<IceServer>
                {
                    new IceServer {Urls = {"stun:stun.l.google.com:19302"}},
                    new IceServer {Urls = {"stun:stun4.l.google.com:19302"}}
                }
            };

            await PeerConnection.InitializeAsync(config);

            PeerConnection.LocalSdpReadytoSend += PeerConnection_LocalSdpReadytoSend;
            PeerConnection.Connected += PeerConnection_Connected;
            PeerConnection.IceStateChanged += PeerConnection_IceStateChanged;
            PeerConnection.IceCandidateReadytoSend += PeerConnection_IceCandidateReadytoSend;
            CaptureChannel = await PeerConnection.AddDataChannelAsync("ScreenCapture", true, true);
            CaptureChannel.BufferingChanged += DataChannel_BufferingChanged;
            CaptureChannel.MessageReceived += CaptureChannel_MessageReceived;
            CaptureChannel.StateChanged += CaptureChannel_StateChanged;
            PeerConnection.CreateOffer();
        }

        public void SendCaptureFrame(int left, int top, int width, int height, byte[] imageBytes)
        {
            for (var i = 0; i < imageBytes.Length; i += 50_000)
                CaptureChannel.SendMessage(MessagePackSerializer.Serialize(new FrameInfo
                {
                    Left = left,
                    Top = top,
                    Width = width,
                    Height = height,
                    EndOfFrame = false,
                    ImageBytes = imageBytes.Skip(i).Take(50_000).ToArray(),
                    DtoType = DynamicDtoType.FrameInfo
                }));
            CaptureChannel.SendMessage(MessagePackSerializer.Serialize(new FrameInfo
            {
                Left = left,
                Top = top,
                Width = width,
                Height = height,
                EndOfFrame = true,
                DtoType = DynamicDtoType.FrameInfo
            }));
        }

        public void SetRemoteDescription(string type, string sdp)
        {
            PeerConnection.SetRemoteDescription(type, sdp);
            if (type == "offer") PeerConnection.CreateAnswer();
        }

        private void CaptureChannel_MessageReceived(byte[] obj)
        {
            Logger.Debug($"DataChannel message received.  Size: {obj.Length}");
        }

        private async void CaptureChannel_StateChanged()
        {
            Logger.Debug($"DataChannel state changed.  New State: {CaptureChannel.State}");
            if (CaptureChannel.State == DataChannel.ChannelState.Closed) await Init();
        }

        private void DataChannel_BufferingChanged(ulong previous, ulong current, ulong limit)
        {
            Logger.Debug(
                $"DataChannel buffering changed.  Previous: {previous}.  Current: {current}.  Limit: {limit}.");
            CurrentBuffer = current;
        }

        private void PeerConnection_Connected()
        {
            Logger.Debug("PeerConnection connected.");
        }

        private void PeerConnection_IceCandidateReadytoSend(string candidate, int sdpMlineindex, string sdpMid)
        {
            Logger.Debug("Ice candidate ready to send.");
            IceCandidateReady?.Invoke(this, (candidate, sdpMlineindex, sdpMid));
        }

        private void PeerConnection_IceStateChanged(IceConnectionState newState)
        {
            Logger.Debug($"Ice state changed to {newState}.");
        }

        private void PeerConnection_LocalSdpReadytoSend(string type, string sdp)
        {
            Logger.Debug("Local SDP ready.");
            LocalSdpReady?.Invoke(this, sdp);
        }
    }
}