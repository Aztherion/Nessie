// Credits to davidluzgouveia, https://github.com/davidluzgouveia, for this code
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using System.Threading;
using System.Diagnostics;

namespace Nessie.Audio
{

    public class Synth
    {
        private Func<float> GetNextSample = null;

        public Synth()
        {
            _audioInstance = new DynamicSoundEffectInstance(SampleRate, AudioChannels.Mono);
            _audioInstance.Play();

            _audioBuffer = new byte[SamplesPerBuffer * BytesPerSample];

            _audioThread = new Thread(Run) { IsBackground = true };
            _audioThread.Start();
        }

        public void SetUserSoundOutFunction(Func<float> getNextSample)
        {
            GetNextSample = getNextSample;
        }

        private void Run()
        {
            try
            {
                var stopwatch = new Stopwatch();
                stopwatch.Start();

                var nextSampleTime = stopwatch.Elapsed.TotalSeconds;
                while (_running)
                {
                    var currenTime = stopwatch.Elapsed.TotalSeconds;

                    if (currenTime > nextSampleTime)
                    {
                        if (GetNextSample != null) 
                        {
                            var sample = GetNextSample();
                            // Console.WriteLine(sample.ToString("F2"));
                            if (sample > 1.0f) sample = 1.0f;
                            if (sample < -1.0f) sample = -1.0f;

                            short pcmSample = (short)(((sample * 2.0f) - 1.0f) * short.MaxValue);
                            
                            _audioBuffer[_bufferIndex++] = (byte)(pcmSample & 0xFF);
                            _audioBuffer[_bufferIndex++] = (byte)((pcmSample >> 8) & 0xFF);

                            if ((_bufferIndex >= _audioBuffer.Length || _audioInstance.PendingBufferCount < 2))
                            {
                                _audioInstance.SubmitBuffer(_audioBuffer, 0, _bufferIndex);
                                _bufferIndex = 0;
                            }
                        }

                        nextSampleTime += SampleDuration;

                        if (nextSampleTime < currenTime)
                        {
                            nextSampleTime = currenTime + SampleDuration;
                        }
                    }
                    // Thread.Yield();
                }

            }
            catch (Exception ex)
            {
                // Console.WriteLine(ex.Message);
            }
        }


        private readonly DynamicSoundEffectInstance _audioInstance;
        private const int SampleRate = 44100;
        private const int SamplesPerBuffer = 1024;
        private const int BytesPerSample = 2; // 16 bit audio
        private byte[] _audioBuffer;
        private Thread _audioThread;
        private bool _running = true;
        private const double SampleDuration = 1.0 / SampleRate;
        private int _bufferIndex;
    }
}
