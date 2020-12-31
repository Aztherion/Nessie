﻿using System;

namespace Nessie
{
    public unsafe class Bus
    {
        public CPU Cpu;
        public PPU Ppu;
        private byte[] _cpuRam = new byte[2048];
        public UInt64 SystemClockCounter;
        private Cartridge _cartridge;
        public byte[] Controller;
        private byte[] _controllerState;

        public Bus()
        {
            InitializeControllers();
            Cpu = new CPU();
            Ppu = new PPU();
            Cpu.ConnectBus(this);
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
            SystemClockCounter = 0;
        }

        public void Clock() 
        {
            Ppu.Clock();
            if (SystemClockCounter % 3 == 0)
            {
                Cpu.Clock(SystemClockCounter);
            }

            if (Ppu.NMI)
            {
                Ppu.NMI = false;
                Cpu.NMI();
            }

            SystemClockCounter++;
        }

        private void InitializeControllers()
        {
            Controller = new byte[2];
            _controllerState = new byte[2];
        }
    }
}
