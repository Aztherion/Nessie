using Nessie.Audio;
using System;
using System.Diagnostics;

namespace Nessie
{
    public class APU
    {
        private UInt32 _clockCounter;
        private UInt32 _frameClockCounter;
        private double _globalTime = 0.0;
        private byte[] _lengthTable = new byte[]
        {
            10, 254, 20, 2, 
            40, 4, 80, 6, 
            160, 8, 60, 10, 
            14, 12, 26, 14, 
            12, 16, 14, 18, 
            48, 20, 96, 22, 
            192, 24, 72, 26, 
            16, 28, 32, 30
        };

        // pulse 1
        /*
        ushort _pulse1Timer;
        ushort _pulse1TimerOrg;
        ushort _pulse1Reload;
        float _pulse1DutyCycle;
        
        byte _pulse1LengthCounter;
        
        bool _pulse1ConstantVolume;
        byte _pulse1Volume;
        byte _pulse1Sequence;
        byte _pulse1Step;
        */
        // Square wave pulse channel 1
        bool _pulse1Enabled;
        bool _pulse1Halt;
        double _pulse1Sample = 0.0;
        double _pulse1Output = 0.0; // the current amplitube (0.0 to 1.0)
        Sequencer _pulse1Seq = new Sequencer();
        OscPulse _pulse1Osc = new OscPulse();
        Envelope _pulse1Env = new Envelope();
        LengthCounter _pulse1LengthCounter = new LengthCounter();
        Sweeper _pulse1Sweep = new Sweeper();

        // Square wave pulse channel 2
        bool _pulse2Enabled;
        bool _pulse2Halt;
        double _pulse2Sample = 0.0;
        double _pulse2Output = 0.0; // the current amplitube (0.0 to 1.0)
        Sequencer _pulse2Seq = new Sequencer();
        OscPulse _pulse2Osc = new OscPulse();
        Envelope _pulse2Env = new Envelope();
        LengthCounter _pulse2LengthCounter = new LengthCounter();
        Sweeper _pulse2Sweep = new Sweeper();

        public double Pulse1Output { get => _pulse1Output; }

        private const int SampleRate = 44100;
        private const int APUClockRate = 50000;
        private const int BufferSize = 1024;

        private float[] _audioBuffer = new float[BufferSize];
        private int _bufferWriteIndex = 0;
        private int _bufferReadIndex = 0;
        private int _bufferCount = 0;

        private double _sampleTimer = 0;

        public APU() {}

        public void CpuWrite(ushort address, byte data) 
        {
            // return;
            switch (address)
            {
                case 0x4000:
                    // DDLC VVVV	Pulse channel 1 Duty (D), envelope loop / length counter halt (L), constant volume (C), volume/envelope (V)
                    switch ((data & 0xC0) >> 6)
                    {
                        case 0x00:
                            _pulse1Seq.NewSequence = 0b01000000;
                            _pulse1Osc.DutyCycle = 0.125F;
                            break;
                        case 0x01:
                            _pulse1Seq.NewSequence = 0b01100000;
                            _pulse1Osc.DutyCycle = 0.250F;
                            break;
                        case 0x02:
                            _pulse1Seq.NewSequence = 0b01111000;
                            _pulse1Osc.DutyCycle = 0.500F;
                            break;
                        case 0x03:
                            _pulse1Seq.NewSequence = 0b10011111;
                            _pulse1Osc.DutyCycle = 0.750F;
                            break;
                    }
                    _pulse1Seq.Sequence = _pulse1Seq.NewSequence;

                    _pulse1Halt = (data & 0x20) != 0;
                    _pulse1Env.Volume = (byte)(data & 0xF);
                    _pulse1Env.Disable = (data & 0x10) != 0;
                    break;
                case 0x4001:
                    // EPPP NSSS	Pulse channel 1 Sweep unit: enabled (E), period (P), negate (N), shift (S)
                    _pulse1Sweep.Enabled = (data & 0x80) != 0;
                    _pulse1Sweep.Period = (byte)((data & 0x70) >> 4);
                    _pulse1Sweep.Down = (data & 0x08) != 0;
                    _pulse1Sweep.Shift = (byte)(data & 0x07);
                    _pulse1Sweep.Reload = true;
                    break;
                case 0x4002:
                    // TTTT TTTT	Pulse channel 1 Timer low (T)
                    //_pulse1Timer = (ushort)(_pulse1Timer | data);
                    //_pulse1TimerOrg = _pulse1Timer;
                    _pulse1Seq.Reload = (ushort)((_pulse1Seq.Reload & 0xFF00) | data);
                    break;
                case 0x4003:
                    // LLLL LTTT	Pulse channel 1 Length counter load (L), timer high (T)
                    _pulse1Seq.Reload = (ushort)(((data & 0x07) << 8) | (_pulse1Seq.Reload & 0x00FF));
                    _pulse1Seq.Timer = _pulse1Seq.Reload;
                    _pulse1Seq.Sequence = _pulse1Seq.NewSequence;
                    _pulse1LengthCounter.Counter = _lengthTable[(data & 0xF8) >> 3];
                    _pulse1Env.Start = true;
                    break;
                case 0x4004:
                    // DDLC VVVV	Pulse channel 2 Duty (D), envelope loop / length counter halt (L), constant volume (C), volume/envelope (V)
                    switch ((data & 0xC0) >> 6)
                    {
                        case 0x00:
                            _pulse2Seq.NewSequence = 0b01000000;
                            _pulse2Osc.DutyCycle = 0.125F;
                            break;
                        case 0x01:
                            _pulse2Seq.NewSequence = 0b01100000;
                            _pulse2Osc.DutyCycle = 0.250F;
                            break;
                        case 0x02:
                            _pulse2Seq.NewSequence = 0b01111000;
                            _pulse2Osc.DutyCycle = 0.500F;
                            break;
                        case 0x03:
                            _pulse2Seq.NewSequence = 0b10011111;
                            _pulse2Osc.DutyCycle = 0.750F;
                            break;
                    }
                    _pulse2Seq.Sequence = _pulse2Seq.NewSequence;

                    _pulse2Halt = (data & 0x20) != 0;
                    _pulse2Env.Volume = (byte)(data & 0xF);
                    _pulse2Env.Disable = (data & 0x10) != 0;
                    break;
                case 0x4005:
                    // EPPP NSSS	Pulse channel 2 Sweep unit: enabled (E), period (P), negate (N), shift (S)
                    _pulse2Sweep.Enabled = (data & 0x80) != 0;
                    _pulse2Sweep.Period = (byte)((data & 0x70) >> 4);
                    _pulse2Sweep.Down = (data & 0x08) != 0;
                    _pulse2Sweep.Shift = (byte)(data & 0x07);
                    _pulse2Sweep.Reload = true;
                    break;
                case 0x4006:
                    // TTTT TTTT	Pulse channel 2 Timer low (T)
                    _pulse2Seq.Reload = (ushort)((_pulse2Seq.Reload & 0xFF00) | data);
                    break;
                case 0x4007:
                    // LLLL LTTT	Pulse channel 2 Length counter load (L), timer high (T)
                    _pulse2Seq.Reload = (ushort)(((data & 0x07) << 8) | (_pulse2Seq.Reload & 0x00FF));
                    _pulse2Seq.Timer = _pulse2Seq.Reload;
                    _pulse2Seq.Sequence = _pulse2Seq.NewSequence;
                    _pulse2LengthCounter.Counter = _lengthTable[(data & 0xF8) >> 3];
                    _pulse2Env.Start = true;
                    break;
                case 0x4008: break;
                case 0x400C: break;
                case 0x400E: break;
                case 0x400F:
                    // LLLL.L---	Noise channel length counter load (write)
                    _pulse1Env.Start = true;
                    _pulse2Env.Start = true;
                    break;
                case 0x4015:
                    // ---D NT21	Enable DMC (D), noise (N), triangle (T), and pulse channels (2/1)
                    _pulse1Enabled = (data & 0x01) != 0;
                    _pulse2Enabled = (data & 0x02) != 0;
                    break;
                case 0x4017:
                    // MI-- ----	Mode (M, 0 = 4-step, 1 = 5-step), IRQ inhibit flag (I)
                    break;
            }
        }

        public byte CpuRead(ushort address)
        {
            byte data = 0x00;
            return data;
        }

        public void Clock()
        {
            var quarterFrameClock = false;
            var halfFrameClock = false;

            _globalTime += (0.3333333333 / 1789773);

            if (_clockCounter % 6 == 0) // 2 CPU Cycles = 1 APU Cycle
            {
                _frameClockCounter++;

                if (_frameClockCounter == 3729)
                {
                    quarterFrameClock = true;
                }

                if (_frameClockCounter == 7457)
                {
                    quarterFrameClock = true;
                    halfFrameClock = true;
                }

                if (_frameClockCounter == 11186)
                {
                    quarterFrameClock = true;
                }

                if (_frameClockCounter == 14916)
                {
                    quarterFrameClock = true;
                    halfFrameClock = true;
                    _frameClockCounter = 0;
                }

                if (quarterFrameClock)
                {
                    _pulse1Env.Clock(_pulse1Halt);
                    _pulse2Env.Clock(_pulse2Halt);
                }

                if (halfFrameClock)
                {
                    _pulse1LengthCounter.Clock(_pulse1Enabled, _pulse1Halt);
                    _pulse2LengthCounter.Clock(_pulse2Enabled, _pulse2Halt);
                    _pulse1Sweep.Clock(ref _pulse1Seq.Reload, false);
                    _pulse2Sweep.Clock(ref _pulse2Seq.Reload, false);
                }

                _pulse1Seq.Clock(_pulse1Enabled, s =>
                {
                    return ((s & 0x0001) << 7) | ((s & 0x00FE) >> 1);
                });

                _pulse1Osc.Frequency = 1789773.0 / (16.0 * (_pulse1Seq.Reload + 1));
                _pulse1Osc.Amplitube = (_pulse1Env.Output - 1) / 16.0;
                _pulse1Sample = _pulse1Osc.Sample(_globalTime);

                if (_pulse1LengthCounter.Counter > 0 && _pulse1Seq.Timer >= 8 && !_pulse1Sweep.Mute && _pulse1Env.Output > 2)
                {
                    _pulse1Output += (_pulse1Sample - _pulse1Output) * 0.5;
                } else
                {
                    _pulse1Output = 0;
                }

                
                _pulse2Seq.Clock(_pulse2Enabled, s =>
                {
                    return ((s & 0x0001) << 7) | ((s & 0x00FE) >> 1);
                });

                _pulse2Osc.Frequency = 1789773.0 / (16.0 * (_pulse2Seq.Reload + 1));
                _pulse2Osc.Amplitube = (_pulse2Env.Output - 1) / 16.0;
                _pulse2Sample = _pulse2Osc.Sample(_globalTime);

                if (_pulse2LengthCounter.Counter > 0 && _pulse2Seq.Timer >= 8 && !_pulse2Sweep.Mute && _pulse2Env.Output > 2)
                {
                    _pulse2Output += (_pulse2Sample - _pulse2Output) * 0.5;
                }
                else
                {
                    _pulse2Output = 0;
                }


                if (!_pulse1Enabled) _pulse1Output = 0;
                if (!_pulse2Enabled) _pulse2Output = 0;
            }

            _pulse1Sweep.Track(_pulse1Seq.Reload);
            _pulse2Sweep.Track(_pulse2Seq.Reload);
            _clockCounter++;
        }

        public void Reset()
        {
            _clockCounter = 0;
            _frameClockCounter = 0;
            _globalTime = 0;
        }

        public double GetOutputSample()
        {
            return ((1.0 * _pulse1Output) - 0.8) * 0.1 +
                ((1.0 * _pulse2Output) - 0.8) * 0.1;
        }
    }

    class OscPulse
    {
        public double Frequency = 0.0;
        public double DutyCycle = 0.0;
        public double Amplitube = 1.0;
        public double Harmonics = 10.0;

        public double Sample(double t)
        {
            var a = 0.0;
            var b = 0.0;
            var p = DutyCycle * 2.0 * Math.PI;

            for (double n = 1; n < Harmonics; n++)
            {
                double c = n * Frequency * 2.0 * Math.PI * t;
                a += -ApproxSin(c) / n;
                b += -ApproxSin(c - p * n) / n;
            }

            return (2.0 * Amplitube / Math.PI) * (a - b);
        }

        private double ApproxSin(double t)
        {
            var j = t * 0.15915;
            j = j - Math.Floor(j);
            return 20.785 * j * (j - 0.5) * (j - 1.0);
        }

    }

    class Envelope
    {
        public bool Start = false;
        public bool Disable = false;
        public ushort DividerCount = 0;
        public ushort Volume = 0;
        public ushort Output = 0;
        public ushort DecayCount = 0;

        public void Clock(bool loop)
        {
            if (!Start)
            {
                if (DividerCount == 0)
                {
                    DividerCount = Volume;
                    if (DecayCount == 0)
                    {
                        if (loop)
                        {
                            DecayCount = 15;
                        }
                    } else
                    {
                        DecayCount--;
                    }
                } else
                {
                    DividerCount--;
                }
            } else
            {
                Start = false;
                DecayCount = 15;
                DividerCount = Volume;
            }

            if (Disable)
            {
                Output = Volume;
            } else
            {
                Output = DecayCount;
            }
        }
    }

    class LengthCounter
    {
        public byte Counter = 0x00;
        public byte Clock(bool enable, bool halt)
        {
            if (!enable)
            {
                Counter = 0;
            } else if (Counter > 0 && !halt)
            {
                Counter--;
            }
            return Counter;
        }
    }

    class Sweeper
    {
        public bool Enabled = false;
        public bool Down = false;
        public bool Reload = false;
        public byte Shift = 0x00;
        public byte Timer = 0x00;
        public byte Period = 0x00;
        public ushort Change = 0;
        public bool Mute = false;

        public void Track(ushort target)
        {
            if (Enabled)
            {
                Change = (ushort)(target >> Shift);
                Mute = (target < 8) || (target > 0x7FF);
            }
        }

        public bool Clock(ref ushort target, bool channel)
        {
            bool changed = false;
            if (Timer == 0 && Enabled && Shift > 0 && !Mute)
            {
                if (target >= 8 && Change < 0x07FF)
                {
                    if (Down)
                    {
                        target = (ushort)(target - (Change - (channel ? 1 : 0)));
                    } else
                    {
                        target = (ushort)(target + Change);
                    }
                    changed = true;
                }
            }

            if (Timer == 0 || Reload)
            {
                Timer = Period;
                Reload = false;
            } else
            {
                Timer--;
            }

            Mute = (target < 8) || (target >= 0x7FF);

            return changed;
        }
    }

    class Sequencer
    {
        public uint Sequence { get; set; } = 0x00000000;
        public uint NewSequence { get; set; } = 0x00000000;
        public ushort Timer { get; set; } = 0x0000;
        public ushort Reload = 0x0000;
        public byte Output { get; set; } = 0x00;

        public byte Clock(bool enable, Func<uint, uint> funcManip)
        {
            if (!enable) return Output;

            Timer--;
            if (Timer == ushort.MaxValue)
            {
                Timer = Reload;
                Sequence = funcManip(Sequence);
                Output = (byte)(Sequence & 0x00000001);
            }

            return Output;
        }
    }
}
