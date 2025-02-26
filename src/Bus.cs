﻿using System;
using System.Threading;

namespace Nessie
{
    public unsafe class Bus
    {
        public CPU Cpu;
        public PPU Ppu;
        public APU Apu;
        public UInt64 SystemClockCounter;
        public byte[] Controller;

        private byte[] _cpuRam = new byte[2048];
        private Cartridge _cartridge;        
        private byte[] _controllerState;

        private byte _dmaPage;
        private byte _dmaAddress;
        private byte _dmaData;

        private bool _dmaTransfer;
        private bool _dmaDummy = true;

        private double _audioTime = 0.0;
        private double _audioGlobalTime = 0.0;
        private double _audioTimePerNESClock = 0.0;
        private double _audioTimePerSystemSample = 0.0;

        public double AudioSample = 0.0;

        private int _clockLockFlag = 0;

        public Bus()
        {
            InitializeControllers();
            Cpu = new CPU();
            Ppu = new PPU();
            Apu = new APU();
            Cpu.ConnectBus(this);
        }

        public void SetSampleFrequency (double sampleRate)
        {
            _audioTimePerSystemSample = 1.0 / sampleRate;
            _audioTimePerNESClock = 1.0 / 5369318.0;
        }

        // Main bus
        public void CpuWrite(ushort address, byte data)
        {
            if (_cartridge.CpuWrite(address, data))
            {
                return;
            }
            else if (address >= 0x0000 && address <= 0x1FFF) 
            {
                _cpuRam[address & 0x07FF] = data;
            }
            else if (address >= 0x2000 && address <= 0x3FFF)
            {
                Ppu.CpuWrite((ushort)(address & 0x0007), data);
            }
            else if ((address >= 0x4000 && address <= 0x4013) || address == 0x4015 || address == 0x4017)
            {
                Apu.CpuWrite((ushort)address, data);
            }
            else if (address == 0x4014)
            {
                _dmaPage = data;
                _dmaAddress = 0x00;
                _dmaTransfer = true;
            }
            else if(address >= 0x4016 && address <= 0x4017)
            {
                _controllerState[(ushort)(address & 0x0001)] = Controller[(ushort)(address & 0x0001)];
            }
        }

        // Main bus
        public byte CpuRead(ushort address)
        {
            byte data = 0x00;

            if (_cartridge.CpuRead(address, ref data))
            {
                return data;
            }
            else if (address >= 0x0000 && address <= 0x1FFF)
            {
                data = _cpuRam[address & 0x07FF];
            }
            else if (address >= 0x2000 && address <= 0x3FFF)
            {
                data = Ppu.CpuRead((ushort)(address & 0x0007));
            } else if (address >= 0x4016 && address <= 0x4017){
                data = (byte)((_controllerState[(ushort)(address & 0x0001)] & 0x80) > 0 ? 1 : 0);
                _controllerState[(ushort)(address & 0x0001)] = (byte)(_controllerState[(ushort)(address & 0x0001)] << 1);

            }

            return data;
        }

        // PPU bus
        public void PpuWrite(ushort address, byte data) { }

        // PPU bus
        public byte PpuRead(ushort address) 
        {
            return 0x00;
        }

        public void InsertCartridge(Cartridge cartridge) 
        {
            _cartridge = cartridge;
            Ppu.ConnectCartridge(cartridge);
        }

        public void Reset() 
        {

            _cartridge.Reset();
            Cpu.Reset();
            Ppu.Reset();
            Apu.Reset();
            SystemClockCounter = 0;
        }

        public bool Clock() 
        {
            if (Interlocked.Exchange(ref _clockLockFlag, 1) == 1)
            {
                return false;
            }

            try
            {
                Ppu.Clock();
                Apu.Clock();
                if (SystemClockCounter % 3 == 0)
                {
                    if (_dmaTransfer)
                    {
                        if (_dmaDummy)
                        {
                            if (SystemClockCounter % 2 == 1)
                            {
                                _dmaDummy = false;
                            }
                        }
                        else
                        {
                            if (SystemClockCounter % 2 == 0)
                            {
                                var dmaAddress = (ushort)((_dmaPage << 8) | _dmaAddress);
                                _dmaData = CpuRead(dmaAddress);
                            }
                            else
                            {
                                Ppu.OAM[_dmaAddress] = _dmaData;
                                _dmaAddress = (byte)(_dmaAddress + 1);
                                if (_dmaAddress == 0x00)
                                {
                                    _dmaTransfer = false;
                                    _dmaDummy = true;
                                }
                            }
                        }
                    }
                    else
                    {
                        Cpu.Clock(SystemClockCounter);
                    }
                }

                var audioSampleReady = false;
                _audioTime += _audioTimePerNESClock;
                if (_audioTime >= _audioTimePerSystemSample)
                {
                    _audioTime -= _audioTimePerSystemSample;
                    AudioSample = Apu.GetOutputSample();
                    audioSampleReady = true;
                }


                if (Ppu.NMI)
                {
                    Ppu.NMI = false;
                    Cpu.NMI();
                }

                SystemClockCounter++;

                return audioSampleReady;
            }
            finally
            {
                Interlocked.Exchange(ref _clockLockFlag, 0);
            }

        }

        private void InitializeControllers()
        {
            Controller = new byte[2];
            _controllerState = new byte[2];
        }
    }
}
