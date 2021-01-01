using Nessie.Audio;
using System;

namespace Nessie
{
    public class APU
    {
        private Synth _synth;
        private UInt32 _clockCounter;
        private UInt32 _frameClockCounter;
        private double _globalTime;
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
        ushort _pulse1Timer;
        ushort _pulse1TimerOrg;
        float _pulse1DutyCycle;
        bool _pulse1Enabled;
        byte _pulse1LengthCounter;
        bool _pulse1Halt;
        bool _pulse1ConstantVolume;
        byte _pulse1Volume;
        byte _pulse1Sequence;

        public APU()
        {
            _synth = new Synth();
        }

        public void CpuWrite(ushort address, byte data) 
        {
            switch (address)
            {
                case 0x4000:
                    // DDLC VVVV	Pulse channel 1 Duty (D), envelope loop / length counter halt (L), constant volume (C), volume/envelope (V)
                    switch ((data & 0xC0) >> 6)
                    {
                        case 0x00:
                            _pulse1Sequence = 0b01000000;
                            _pulse1DutyCycle = 0.125F;
                            break;
                        case 0x01:
                            _pulse1Sequence = 0b01100000;
                            _pulse1DutyCycle = 0.250F;
                            break;
                        case 0x02:
                            _pulse1Sequence = 0b01111000;
                            _pulse1DutyCycle = 0.500F;
                            break;
                        case 0x03:
                            _pulse1Sequence = 0b10011111;
                            _pulse1DutyCycle = 0.750F;
                            break;
                    }
                    _pulse1Halt = (data & 0x20) > 0;
                    _pulse1ConstantVolume = (data & 0x10) > 0;
                    _pulse1Volume = (byte)(data & 0xF);
                    break;
                case 0x4001:
                    // EPPP NSSS	Pulse channel 1 Sweep unit: enabled (E), period (P), negate (N), shift (S)
                    break;
                case 0x4002:
                    // TTTT TTTT	Pulse channel 1 Timer low (T)
                    _pulse1Timer = (ushort)(_pulse1Timer | data);
                    _pulse1TimerOrg = _pulse1Timer;
                    break;
                case 0x4003:
                    // LLLL LTTT	Pulse channel 1 Length counter load (L), timer high (T)
                    _pulse1Timer = (ushort)(_pulse1Timer | (data << 8));
                    _pulse1TimerOrg = _pulse1Timer;
                    _pulse1LengthCounter = (byte)(data >> 2);
                    break;
                case 0x4004:
                    // DDLC VVVV	Pulse channel 2 Duty (D), envelope loop / length counter halt (L), constant volume (C), volume/envelope (V)
                    break;
                case 0x4005:
                    // EPPP NSSS	Pulse channel 2 Sweep unit: enabled (E), period (P), negate (N), shift (S)
                    break;
                case 0x4006:
                    // TTTT TTTT	Pulse channel 2 Timer low (T)
                    break;
                case 0x4007:
                    // LLLL LTTT	Pulse channel 2 Length counter load (L), timer high (T)
                    break;
                case 0x4008: break;
                case 0x400C: break;
                case 0x400E: break;
                case 0x400F:
                    // LLLL.L---	Noise channel length counter load (write)
                    break;
                case 0x4015:
                    // ---D NT21	Enable DMC (D), noise (N), triangle (T), and pulse channels (2/1)
                    _pulse1Enabled = (data & 0x1) > 0;
                    if (!_pulse1Enabled)
                    {
                        _pulse1LengthCounter = 0;
                    }
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

            _globalTime += (0.3333333333 / 1662607);

            if (_clockCounter % 6 == 0) // 2 CPU Cycles = 1 APU Cycle
            {
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

                if (halfFrameClock)
                {
                    if (_pulse1LengthCounter > 0 && _pulse1Enabled)
                    {
                        _pulse1LengthCounter = (byte)(_pulse1LengthCounter - 1);

                    }
                }

                if (_pulse1Timer > 0)
                {
                    _pulse1Timer = (ushort)(_pulse1Timer - 1);
                } else
                {
                    _pulse1Timer = _pulse1TimerOrg;
                }
            }

            _clockCounter++;
        }

        public void Reset()
        {
            _clockCounter = 0;
            _frameClockCounter = 0;
            _globalTime = 0;
        }

        // Debug
        public void PlayTone()
        {
            _synth.VoiceOn(0, 440);
        }

        public void StopTone()
        {
            _synth.VoiceOff(0);
        }
    }
}
