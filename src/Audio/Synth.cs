﻿// Credits to davidluzgouveia, https://github.com/davidluzgouveia, for this code
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;

namespace Nessie.Audio
{

    public class Synth
    {
        public Synth()
        {
            // Create DynamicSoundEffectInstance object and start it
            _instance = new DynamicSoundEffectInstance(SampleRate, Channels == 2 ? AudioChannels.Stereo : AudioChannels.Mono);
            _instance.Play();

            // Create buffers
            const int bytesPerSample = 2;
            _xnaBuffer = new byte[Channels * SamplesPerBuffer * bytesPerSample];
            _workingBuffer = new float[Channels, SamplesPerBuffer];

            // Create voice structures
            _voicePool = new Voice[Polyphony];
            _voicePool[0] = new Voice(this, Audio.Oscillator.Square);
            _voicePool[1] = new Voice(this, Audio.Oscillator.Square);
            _voicePool[2] = new Voice(this, Audio.Oscillator.Triangle);
            _voicePool[3] = new Voice(this, Audio.Oscillator.WhiteNoise);

            _freeVoices = new Stack<Voice>(_voicePool);
            _activeVoices = new List<Voice>();
            _keyRegistry = new Dictionary<int, Voice>();
        }

        // Synth Parameters

        public const int Channels = 1;
        public const int SampleRate = 44100;
        public const int SamplesPerBuffer = 5000; // Tweak this for latency
        public const int Polyphony = 4;
        public int FadeInDuration = 200;
        public int FadeOutDuration = 200;
        //public OscillatorDelegate Oscillator = Audio.Oscillator.Sine;

        // Public Methods

        public void Update()
        {
            while (_instance.PendingBufferCount < 2)
            {
                SubmitBuffer();
            }
        }

        public void VoiceOn(int voiceId, float frequency)
        {
            var voice = _voicePool[voiceId];
            voice.Start(frequency);
            _activeVoices.Add(voice);
        }

        public void VoiceOff(int voiceId)
        {
            var voice = _voicePool[voiceId];
            voice.Stop();
        }

        public void NoteOn(int note)
        {
            if (_keyRegistry.ContainsKey(note))
            {
                return;
            }

            Voice freeVoice = GetFreeVoice();

            if (freeVoice == null)
            {
                return;
            }

            _keyRegistry[note] = freeVoice;

            freeVoice.Start(SoundHelper.NoteToFrequency(note));

            _activeVoices.Add(freeVoice);
        }

        public void NoteOff(int note)
        {
            Voice voice;
            if (_keyRegistry.TryGetValue(note, out voice))
            {
                voice.Stop();
                _keyRegistry.Remove(note);
            }
        }

        // Private Methods

        private void SubmitBuffer()
        {
            ClearWorkingBuffer();
            FillWorkingBuffer();
            SoundHelper.ConvertBuffer(_workingBuffer, _xnaBuffer);
            _instance.SubmitBuffer(_xnaBuffer);
        }

        private void ClearWorkingBuffer()
        {
            Array.Clear(_workingBuffer, 0, Channels * SamplesPerBuffer);
        }

        private void FillWorkingBuffer()
        {
            // Call Process on all active voices
            for (int i = _activeVoices.Count - 1; i >= 0; --i)
            {
                Voice voice = _activeVoices[i];
                voice.Process(_workingBuffer);

                // Remove any voices that stopped being active
                if (!voice.IsAlive)
                {
                    _activeVoices.RemoveAt(i);
                    _freeVoices.Push(voice);
                }
            }
        }

        private Voice GetFreeVoice()
        {
            if (_freeVoices.Count == 0)
            {
                return null;
            }

            return _freeVoices.Pop();
        }

        private readonly DynamicSoundEffectInstance _instance;
        private readonly byte[] _xnaBuffer;
        private readonly float[,] _workingBuffer;

        private readonly Voice[] _voicePool;
        private readonly List<Voice> _activeVoices;
        private readonly Stack<Voice> _freeVoices;
        private readonly Dictionary<int, Voice> _keyRegistry;

        // For Debug

        public int ActiveVoicesCount { get { return _activeVoices.Count; } }
        public int FreeVoicesCount { get { return _freeVoices.Count; } }
        public int KeyRegistryCount { get { return _keyRegistry.Count; } }
    }
}
