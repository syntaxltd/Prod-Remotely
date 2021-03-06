﻿using NAudio.Wave;

namespace Silgred.ScreenCast.Win.Audio
{
    /// <summary>
    ///     DSP Group TrueSpeech codec, using ACM
    ///     n.b. Windows XP came with a TrueSpeech codec built in
    ///     - looks like Windows 7 doesn't
    /// </summary>
    internal class TrueSpeechChatCodec : AcmChatCodec
    {
        public TrueSpeechChatCodec()
            : base(new WaveFormat(8000, 16, 1), new TrueSpeechWaveFormat())
        {
        }

        public override string Name => "DSP Group TrueSpeech";
    }
}