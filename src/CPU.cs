using System;
using System.Diagnostics;

namespace Nessie
{
    unsafe public class CPU
    {
        public ushort PC; // program counter
        public byte SP;   // stack pointer
        public byte A; // Accumulator
        public byte X; // Index register X
        public byte Y; // Index register Y
        public ProcessStatusRegister P;
        public bool Complete => Cycles <= 0;
        public string CurrentInstruction { get; private set; }

        public delegate void OpcodeDelegate();

        OpcodeDelegate[] OpcodeFuncPtrTable = new OpcodeDelegate[0x100];
        private const ushort NonMaskableInterruptVector = 0xFFFA;
        private const ushort ResetVector = 0xFFFC;
        private const ushort InterruptVector = 0xFFFE;
        private Bus Bus;
        private int Cycles;
        private long InstructionCount = 1;
        public CPU()
        {
            InitializeInstructionTable();
            CurrentInstruction = string.Empty;
        }

        public void ConnectBus(Bus bus)
        {
            Bus = bus;
        }

        public void Print()
        {
            Console.WriteLine("============================================================================================");
            Console.WriteLine($"SP:${SP:X}\tPC:${PC:X}\tA:${A:X}\tX:${X:X}\tY:${Y:X}");
            P.Print();
            Console.WriteLine("============================================================================================");
        }

        #region Boot Init Execute
        public void Reset()
        {
            SP = 0xFD;
            PC = ReadWord(ResetVector);
            //PC = 0xC000;
            A = 0;
            X = 0;
            Y = 0;
            P.Reset();
            Cycles = 8;
            CurrentInstruction = "RST";
        }

        public void NMI()
        {
            var pcl = (byte)PC;
            var pch = (byte)(PC >> 8);
            var status = P.Get();
            status = (byte)(status | 0b10000);
            status = (byte)(status & ~(1 << 4));
            Push(pch);
            Push(pcl);
            Push(status);
            PC = ReadWord(NonMaskableInterruptVector);
            P.B = false;
            P.I = true;
            Cycles = 8;
            CurrentInstruction = "NMI";
        }

        public void IRQ()
        {
            if (P.I) return;
            var pcl = (byte)PC;
            var pch = (byte)(PC >> 8);
            var status = P.Get();
            status = (byte)(status | 0b10000);
            status = (byte)(status & ~(1 << 4));
            Push(pch);
            Push(pcl);
            Push(status);
            PC = ReadWord(InterruptVector);
            P.B = false;
            P.I = true;
            Cycles = 7;
            CurrentInstruction = "IRQ";
        }

        private void InitializeInstructionTable()
        {
            for (var i = 0; i < 0x100; i++)
            {
                OpcodeFuncPtrTable[i] = InvalidOperationException;
            }

            OpcodeFuncPtrTable[(int)Opcode.LDA_IM] = LDA_IM;
            OpcodeFuncPtrTable[(int)Opcode.LDA_ZP] = LDA_ZP;
            OpcodeFuncPtrTable[(int)Opcode.LDA_ZPX] = LDA_ZPX;
            OpcodeFuncPtrTable[(int)Opcode.LDA_ABS] = LDA_ABS;
            OpcodeFuncPtrTable[(int)Opcode.LDA_ABS_X] = LDA_ABS_X;
            OpcodeFuncPtrTable[(int)Opcode.LDA_ABS_Y] = LDA_ABS_Y;
            OpcodeFuncPtrTable[(int)Opcode.LDA_IND_X] = LDA_IND_X;
            OpcodeFuncPtrTable[(int)Opcode.LDA_IND_Y] = LDA_IND_Y;
            OpcodeFuncPtrTable[(int)Opcode.LDX_IM] = LDX_IM;
            OpcodeFuncPtrTable[(int)Opcode.LDX_ZP] = LDX_ZP;
            OpcodeFuncPtrTable[(int)Opcode.LDX_ZPY] = LDX_ZPY;
            OpcodeFuncPtrTable[(int)Opcode.LDX_ABS] = LDX_ABS;
            OpcodeFuncPtrTable[(int)Opcode.LDX_ABS_Y] = LDX_ABS_Y;
            OpcodeFuncPtrTable[(int)Opcode.LDY_IM] = LDY_IM;
            OpcodeFuncPtrTable[(int)Opcode.LDY_ZP] = LDY_ZP;
            OpcodeFuncPtrTable[(int)Opcode.LDY_ZPX] = LDY_ZPX;
            OpcodeFuncPtrTable[(int)Opcode.LDY_ABS] = LDY_ABS;
            OpcodeFuncPtrTable[(int)Opcode.LDY_ABS_X] = LDY_ABS_X;
            OpcodeFuncPtrTable[(int)Opcode.STA_ZP] = STA_ZP;
            OpcodeFuncPtrTable[(int)Opcode.STA_ZPX] = STA_ZPX;
            OpcodeFuncPtrTable[(int)Opcode.STA_ABS] = STA_ABS;
            OpcodeFuncPtrTable[(int)Opcode.STA_ABS_X] = STA_ABS_X;
            OpcodeFuncPtrTable[(int)Opcode.STA_ABS_Y] = STA_ABS_Y;
            OpcodeFuncPtrTable[(int)Opcode.STA_IND_X] = STA_IND_X;
            OpcodeFuncPtrTable[(int)Opcode.STA_IND_Y] = STA_IND_Y;
            OpcodeFuncPtrTable[(int)Opcode.STX_ZP] = STX_ZP;
            OpcodeFuncPtrTable[(int)Opcode.STX_ZPY] = STX_ZPY;
            OpcodeFuncPtrTable[(int)Opcode.STX_ABS] = STX_ABS;
            OpcodeFuncPtrTable[(int)Opcode.STY_ZP] = STY_ZP;
            OpcodeFuncPtrTable[(int)Opcode.STY_ZPX] = STY_ZPX;
            OpcodeFuncPtrTable[(int)Opcode.STY_ABS] = STY_ABS;
            OpcodeFuncPtrTable[(int)Opcode.TAX] = TAX;
            OpcodeFuncPtrTable[(int)Opcode.TAY] = TAY;
            OpcodeFuncPtrTable[(int)Opcode.TXA] = TXA;
            OpcodeFuncPtrTable[(int)Opcode.TYA] = TYA;
            OpcodeFuncPtrTable[(int)Opcode.TSX] = TSX;
            OpcodeFuncPtrTable[(int)Opcode.TXS] = TXS;
            OpcodeFuncPtrTable[(int)Opcode.PHA] = PHA;
            OpcodeFuncPtrTable[(int)Opcode.PHP] = PHP;
            OpcodeFuncPtrTable[(int)Opcode.PLA] = PLA;
            OpcodeFuncPtrTable[(int)Opcode.PLP] = PLP;
            OpcodeFuncPtrTable[(int)Opcode.AND_IM] = AND_IM;
            OpcodeFuncPtrTable[(int)Opcode.AND_ZP] = AND_ZP;
            OpcodeFuncPtrTable[(int)Opcode.AND_ZPX] = AND_ZPX;
            OpcodeFuncPtrTable[(int)Opcode.AND_ABS] = AND_ABS;
            OpcodeFuncPtrTable[(int)Opcode.AND_ABS_X] = AND_ABS_X;
            OpcodeFuncPtrTable[(int)Opcode.AND_ABS_Y] = AND_ABS_Y;
            OpcodeFuncPtrTable[(int)Opcode.AND_IND_X] = AND_IND_X;
            OpcodeFuncPtrTable[(int)Opcode.AND_IND_Y] = AND_IND_Y;
            OpcodeFuncPtrTable[(int)Opcode.EOR_IM] = EOR_IM;
            OpcodeFuncPtrTable[(int)Opcode.EOR_ZP] = EOR_ZP;
            OpcodeFuncPtrTable[(int)Opcode.EOR_ZPX] = EOR_ZPX;
            OpcodeFuncPtrTable[(int)Opcode.EOR_ABS] = EOR_ABS;
            OpcodeFuncPtrTable[(int)Opcode.EOR_ABS_X] = EOR_ABS_X;
            OpcodeFuncPtrTable[(int)Opcode.EOR_ABS_Y] = EOR_ABS_Y;
            OpcodeFuncPtrTable[(int)Opcode.EOR_IND_X] = EOR_IND_X;
            OpcodeFuncPtrTable[(int)Opcode.EOR_IND_Y] = EOR_IND_Y;
            OpcodeFuncPtrTable[(int)Opcode.ORA_IM] = ORA_IM;
            OpcodeFuncPtrTable[(int)Opcode.ORA_ZP] = ORA_ZP;
            OpcodeFuncPtrTable[(int)Opcode.ORA_ZPX] = ORA_ZPX;
            OpcodeFuncPtrTable[(int)Opcode.ORA_ABS] = ORA_ABS;
            OpcodeFuncPtrTable[(int)Opcode.ORA_ABS_X] = ORA_ABS_X;
            OpcodeFuncPtrTable[(int)Opcode.ORA_ABS_Y] = ORA_ABS_Y;
            OpcodeFuncPtrTable[(int)Opcode.ORA_IND_X] = ORA_IND_X;
            OpcodeFuncPtrTable[(int)Opcode.ORA_IND_Y] = ORA_IND_Y;
            OpcodeFuncPtrTable[(int)Opcode.BIT_ZP] = BIT_ZP;
            OpcodeFuncPtrTable[(int)Opcode.BIT_ABS] = BIT_ABS;
            OpcodeFuncPtrTable[(int)Opcode.ADC_IM] = ADC_IM;
            OpcodeFuncPtrTable[(int)Opcode.ADC_ZP] = ADC_ZP;
            OpcodeFuncPtrTable[(int)Opcode.ADC_ZPX] = ADC_ZPX;
            OpcodeFuncPtrTable[(int)Opcode.ADC_ABS] = ADC_ABS;
            OpcodeFuncPtrTable[(int)Opcode.ADC_ABS_X] = ADC_ABS_X;
            OpcodeFuncPtrTable[(int)Opcode.ADC_ABS_Y] = ADC_ABS_Y;
            OpcodeFuncPtrTable[(int)Opcode.ADC_IND_X] = ADC_IND_X;
            OpcodeFuncPtrTable[(int)Opcode.ADC_IND_Y] = ADC_IND_Y;
            OpcodeFuncPtrTable[(int)Opcode.SBC_IM] = SBC_IM;
            OpcodeFuncPtrTable[(int)Opcode.SBC_IM_DUPLICATE] = SBC_IM;
            OpcodeFuncPtrTable[(int)Opcode.SBC_ZP] = SBC_ZP;
            OpcodeFuncPtrTable[(int)Opcode.SBC_ZPX] = SBC_ZPX;
            OpcodeFuncPtrTable[(int)Opcode.SBC_ABS] = SBC_ABS;
            OpcodeFuncPtrTable[(int)Opcode.SBC_ABS_X] = SBC_ABS_X;
            OpcodeFuncPtrTable[(int)Opcode.SBC_ABS_Y] = SBC_ABS_Y;
            OpcodeFuncPtrTable[(int)Opcode.SBC_IND_X] = SBC_IND_X;
            OpcodeFuncPtrTable[(int)Opcode.SBC_IND_Y] = SBC_IND_Y;
            OpcodeFuncPtrTable[(int)Opcode.CMP_IM] = CMP_IM;
            OpcodeFuncPtrTable[(int)Opcode.CMP_ZP] = CMP_ZP;
            OpcodeFuncPtrTable[(int)Opcode.CMP_ZPX] = CMP_ZPX;
            OpcodeFuncPtrTable[(int)Opcode.CMP_ABS] = CMP_ABS;
            OpcodeFuncPtrTable[(int)Opcode.CMP_ABS_X] = CMP_ABS_X;
            OpcodeFuncPtrTable[(int)Opcode.CMP_ABS_Y] = CMP_ABS_Y;
            OpcodeFuncPtrTable[(int)Opcode.CMP_IND_X] = CMP_IND_X;
            OpcodeFuncPtrTable[(int)Opcode.CMP_IND_Y] = CMP_IND_Y;
            OpcodeFuncPtrTable[(int)Opcode.CPX_IM] = CPX_IM;
            OpcodeFuncPtrTable[(int)Opcode.CPX_ZP] = CPX_ZP;
            OpcodeFuncPtrTable[(int)Opcode.CPX_ABS] = CPX_ABS;
            OpcodeFuncPtrTable[(int)Opcode.CPY_IM] = CPY_IM;
            OpcodeFuncPtrTable[(int)Opcode.CPY_ZP] = CPY_ZP;
            OpcodeFuncPtrTable[(int)Opcode.CPY_ABS] = CPY_ABS;
            OpcodeFuncPtrTable[(int)Opcode.INC_ZP] = INC_ZP;
            OpcodeFuncPtrTable[(int)Opcode.INC_ZPX] = INC_ZPX;
            OpcodeFuncPtrTable[(int)Opcode.INC_ABS] = INC_ABS;
            OpcodeFuncPtrTable[(int)Opcode.INC_ABS_X] = INC_ABS_X;
            OpcodeFuncPtrTable[(int)Opcode.INX] = INX;
            OpcodeFuncPtrTable[(int)Opcode.INY] = INY;
            OpcodeFuncPtrTable[(int)Opcode.DEC_ZP] = DEC_ZP;
            OpcodeFuncPtrTable[(int)Opcode.DEC_ZPX] = DEC_ZPX;
            OpcodeFuncPtrTable[(int)Opcode.DEC_ABS] = DEC_ABS;
            OpcodeFuncPtrTable[(int)Opcode.DEC_ABS_X] = DEC_ABS_X;
            OpcodeFuncPtrTable[(int)Opcode.DEX] = DEX;
            OpcodeFuncPtrTable[(int)Opcode.DEY] = DEY;
            OpcodeFuncPtrTable[(int)Opcode.ASL_ACC] = ASL_ACC;
            OpcodeFuncPtrTable[(int)Opcode.ASL_ZP] = ASL_ZP;
            OpcodeFuncPtrTable[(int)Opcode.ASL_ZPX] = ASL_ZPX;
            OpcodeFuncPtrTable[(int)Opcode.ASL_ABS] = ASL_ABS;
            OpcodeFuncPtrTable[(int)Opcode.ASL_ABS_X] = ASL_ABS_X;
            OpcodeFuncPtrTable[(int)Opcode.LSR_ACC] = LSR_ACC;
            OpcodeFuncPtrTable[(int)Opcode.LSR_ZP] = LSR_ZP;
            OpcodeFuncPtrTable[(int)Opcode.LSR_ZPX] = LSR_ZPX;
            OpcodeFuncPtrTable[(int)Opcode.LSR_ABS] = LSR_ABS;
            OpcodeFuncPtrTable[(int)Opcode.LSR_ABS_X] = LSR_ABS_X;
            OpcodeFuncPtrTable[(int)Opcode.ROL_ACC] = ROL_ACC;
            OpcodeFuncPtrTable[(int)Opcode.ROL_ZP] = ROL_ZP;
            OpcodeFuncPtrTable[(int)Opcode.ROL_ZPX] = ROL_ZPX;
            OpcodeFuncPtrTable[(int)Opcode.ROL_ABS] = ROL_ABS;
            OpcodeFuncPtrTable[(int)Opcode.ROL_ABS_X] = ROL_ABS_X;
            OpcodeFuncPtrTable[(int)Opcode.ROR_ACC] = ROR_ACC;
            OpcodeFuncPtrTable[(int)Opcode.ROR_ZP] = ROR_ZP;
            OpcodeFuncPtrTable[(int)Opcode.ROR_ZPX] = ROR_ZPX;
            OpcodeFuncPtrTable[(int)Opcode.ROR_ABS] = ROR_ABS;
            OpcodeFuncPtrTable[(int)Opcode.ROR_ABS_X] = ROR_ABS_X;
            OpcodeFuncPtrTable[(int)Opcode.JMP_ABS] = JMP_ABS;
            OpcodeFuncPtrTable[(int)Opcode.JMP_IND] = JMP_IND;
            OpcodeFuncPtrTable[(int)Opcode.JSR] = JSR;
            OpcodeFuncPtrTable[(int)Opcode.RTS] = RTS;
            OpcodeFuncPtrTable[(int)Opcode.BCC] = BCC;
            OpcodeFuncPtrTable[(int)Opcode.BCS] = BCS;
            OpcodeFuncPtrTable[(int)Opcode.BEQ] = BEQ;
            OpcodeFuncPtrTable[(int)Opcode.BMI] = BMI;
            OpcodeFuncPtrTable[(int)Opcode.BNE] = BNE;
            OpcodeFuncPtrTable[(int)Opcode.BPL] = BPL;
            OpcodeFuncPtrTable[(int)Opcode.BVC] = BVC;
            OpcodeFuncPtrTable[(int)Opcode.BVS] = BVS;
            OpcodeFuncPtrTable[(int)Opcode.CLC] = CLC;
            OpcodeFuncPtrTable[(int)Opcode.CLD] = CLD;
            OpcodeFuncPtrTable[(int)Opcode.CLI] = CLI;
            OpcodeFuncPtrTable[(int)Opcode.CLV] = CLV;
            OpcodeFuncPtrTable[(int)Opcode.SEC] = SEC;
            OpcodeFuncPtrTable[(int)Opcode.SED] = SED;
            OpcodeFuncPtrTable[(int)Opcode.SEI] = SEI;
            OpcodeFuncPtrTable[(int)Opcode.BRK] = BRK;
            OpcodeFuncPtrTable[(int)Opcode.NOP] = NOP;
            OpcodeFuncPtrTable[(int)Opcode.RTI] = RTI;

            OpcodeFuncPtrTable[(int)Opcode.LAX_IND_X] = LAX_IND_X;
            OpcodeFuncPtrTable[(int)Opcode.LAX_IND_Y] = LAX_IND_Y;
            OpcodeFuncPtrTable[(int)Opcode.LAX_ZP] = LAX_ZP;
            OpcodeFuncPtrTable[(int)Opcode.LAX_ABS] = LAX_ABS;
            OpcodeFuncPtrTable[(int)Opcode.LAX_ABS_Y] = LAX_ABS_Y;
            OpcodeFuncPtrTable[(int)Opcode.LAX_ZPY] = LAX_ZPY;

            OpcodeFuncPtrTable[(int)Opcode.SAX_IND_X] = SAX_IND_X;
            OpcodeFuncPtrTable[(int)Opcode.SAX_IM] = SAX_IM;
            OpcodeFuncPtrTable[(int)Opcode.SAX_ZPY] = SAX_ZPY;
            OpcodeFuncPtrTable[(int)Opcode.SAX_ABS] = SAX_ABS;

            OpcodeFuncPtrTable[(int)Opcode.DCP_IND_X] = DCP_IND_X;
            OpcodeFuncPtrTable[(int)Opcode.DCP_IND_Y] = DCP_IND_Y;
            OpcodeFuncPtrTable[(int)Opcode.DCP_ZP] = DCP_ZP;
            OpcodeFuncPtrTable[(int)Opcode.DCP_ZP_X] = DCP_ZP_X;
            OpcodeFuncPtrTable[(int)Opcode.DCP_ABS] = DCP_ABS;
            OpcodeFuncPtrTable[(int)Opcode.DCP_ABS_Y] = DCP_ABS_Y;
            OpcodeFuncPtrTable[(int)Opcode.DCP_ABS_X] = DCP_ABS_X;

            OpcodeFuncPtrTable[(int)Opcode.ISB_ZP] = ISB_ZP;
            OpcodeFuncPtrTable[(int)Opcode.ISB_ZPX] = ISB_ZPX;
            OpcodeFuncPtrTable[(int)Opcode.ISB_ABS] = ISB_ABS;
            OpcodeFuncPtrTable[(int)Opcode.ISB_ABS_X] = ISB_ABS_X;
            OpcodeFuncPtrTable[(int)Opcode.ISB_ABS_Y] = ISB_ABS_Y;
            OpcodeFuncPtrTable[(int)Opcode.ISB_IND_X] = ISB_IND_X;
            OpcodeFuncPtrTable[(int)Opcode.ISB_IND_Y] = ISB_IND_Y;

            OpcodeFuncPtrTable[(int)Opcode.SLO_ZP] = SLO_ZP;
            OpcodeFuncPtrTable[(int)Opcode.SLO_ZPX] = SLO_ZPX;
            OpcodeFuncPtrTable[(int)Opcode.SLO_ABS] = SLO_ABS;
            OpcodeFuncPtrTable[(int)Opcode.SLO_ABS_X] = SLO_ABS_X;
            OpcodeFuncPtrTable[(int)Opcode.SLO_ABS_Y] = SLO_ABS_Y;
            OpcodeFuncPtrTable[(int)Opcode.SLO_IND_X] = SLO_IND_X;
            OpcodeFuncPtrTable[(int)Opcode.SLO_IND_Y] = SLO_IND_Y;

            OpcodeFuncPtrTable[(int)Opcode.RLA_ZP] = RLA_ZP;
            OpcodeFuncPtrTable[(int)Opcode.RLA_ZPX] = RLA_ZPX;
            OpcodeFuncPtrTable[(int)Opcode.RLA_ABS] = RLA_ABS;
            OpcodeFuncPtrTable[(int)Opcode.RLA_ABS_X] = RLA_ABS_X;
            OpcodeFuncPtrTable[(int)Opcode.RLA_ABS_Y] = RLA_ABS_Y;
            OpcodeFuncPtrTable[(int)Opcode.RLA_IND_X] = RLA_IND_X;
            OpcodeFuncPtrTable[(int)Opcode.RLA_IND_Y] = RLA_IND_Y;

            OpcodeFuncPtrTable[(int)Opcode.SRE_ZP] = SRE_ZP;
            OpcodeFuncPtrTable[(int)Opcode.SRE_ZPX] = SRE_ZPX;
            OpcodeFuncPtrTable[(int)Opcode.SRE_ABS] = SRE_ABS;
            OpcodeFuncPtrTable[(int)Opcode.SRE_ABS_X] = SRE_ABS_X;
            OpcodeFuncPtrTable[(int)Opcode.SRE_ABS_Y] = SRE_ABS_Y;
            OpcodeFuncPtrTable[(int)Opcode.SRE_IND_X] = SRE_IND_X;
            OpcodeFuncPtrTable[(int)Opcode.SRE_IND_Y] = SRE_IND_Y;

            OpcodeFuncPtrTable[(int)Opcode.RRA_ZP] = RRA_ZP;
            OpcodeFuncPtrTable[(int)Opcode.RRA_ZPX] = RRA_ZPX;
            OpcodeFuncPtrTable[(int)Opcode.RRA_ABS] = RRA_ABS;
            OpcodeFuncPtrTable[(int)Opcode.RRA_ABS_X] = RRA_ABS_X;
            OpcodeFuncPtrTable[(int)Opcode.RRA_ABS_Y] = RRA_ABS_Y;
            OpcodeFuncPtrTable[(int)Opcode.RRA_IND_X] = RRA_IND_X;
            OpcodeFuncPtrTable[(int)Opcode.RRA_IND_Y] = RRA_IND_Y;

            OpcodeFuncPtrTable[0x04] = InvalidOperationOneByteNineCycles;
            OpcodeFuncPtrTable[0x44] = InvalidOperationOneByteNineCycles;
            OpcodeFuncPtrTable[0x64] = InvalidOperationOneByteNineCycles;
            OpcodeFuncPtrTable[0x0C] = InvalidOperationTwoBytesTwelveCycles;
            OpcodeFuncPtrTable[0x80] = InvalidOperationOneByteSixCycles;

            OpcodeFuncPtrTable[0x14] = InvalidOperationOneByteTwelveCycles;
            OpcodeFuncPtrTable[0x34] = InvalidOperationOneByteTwelveCycles;
            OpcodeFuncPtrTable[0x54] = InvalidOperationOneByteTwelveCycles;
            OpcodeFuncPtrTable[0x74] = InvalidOperationOneByteTwelveCycles;
            OpcodeFuncPtrTable[0xD4] = InvalidOperationOneByteTwelveCycles;
            OpcodeFuncPtrTable[0xF4] = InvalidOperationOneByteTwelveCycles;

            OpcodeFuncPtrTable[0x1C] = InvalidOperationTwoBytesFifteenCycles;
            OpcodeFuncPtrTable[0x3C] = InvalidOperationTwoBytesFifteenCycles;
            OpcodeFuncPtrTable[0x5C] = InvalidOperationTwoBytesFifteenCycles;
            OpcodeFuncPtrTable[0x7C] = InvalidOperationTwoBytesFifteenCycles;
            OpcodeFuncPtrTable[0xDC] = InvalidOperationTwoBytesFifteenCycles;
            OpcodeFuncPtrTable[0xFC] = InvalidOperationTwoBytesFifteenCycles;
            
            
            
            OpcodeFuncPtrTable[0x1A] = InvalidOperationZeroBytesSixCycles;
            OpcodeFuncPtrTable[0x3A] = InvalidOperationZeroBytesSixCycles;
            OpcodeFuncPtrTable[0x5A] = InvalidOperationZeroBytesSixCycles;
            OpcodeFuncPtrTable[0x7A] = InvalidOperationZeroBytesSixCycles;
            OpcodeFuncPtrTable[0xDA] = InvalidOperationZeroBytesSixCycles;
            OpcodeFuncPtrTable[0xFA] = InvalidOperationZeroBytesSixCycles;
        }

        public void Clock(ulong systemClock)
        {
            if (Cycles <= 0)
            {
                //Console.WriteLine();
                //Console.Write($"{InstructionCount}\t{PC:X} ");
                
                var a = A.ToString("X");
                var x = X.ToString("X");
                var y = Y.ToString("X");
                var sp = SP.ToString("X");
                var p = P.Get().ToString("X");
                InstructionCount++;
                if (InstructionCount == 8990)
                {
                    if (Debugger.IsAttached)
                    {
                        //Debugger.Break();
                    }
                }
                var opcode = ReadByte();
                OpcodeFuncPtrTable[opcode]();
                //Console.Write($"\t\tA:{a} X:{x} Y:{y} P:{p} SP:{sp} CYC:{((systemClock-24)%341),3}\r\n");
            }
            else
            {
                //Console.Write(".");
            }
            Cycles--;
        }
        #endregion

        #region Memory stuff
        /// <summary>
        /// Reads a byte from the current PC address. Increments PC. Decreases cycles by one.
        /// </summary>
        /// <param name="memory"></param>
        /// <param name="cycles"></param>
        /// <returns></returns>
        private byte ReadByte()
        {
            var data = ReadByte(PC);
            PC++;
            //Console.Write($" {data:X}");
            return data;
        }

        /// <summary>
        /// Reads a byte from the specified address. Decreases cycles by one. 
        /// </summary>
        /// <param name="memory"></param>
        /// <param name="address"></param>
        /// <param name="cycles"></param>
        /// <returns></returns>
        private byte ReadByte(ushort address)
        {
            var data = Bus.CpuRead(address);
            //Console.Write($" {data:X}");
            return data;
        }

        /// <summary>
        /// Reads a word from the current PC address. Increments PC by two. Decreases cycles by two.
        /// </summary>
        /// <param name="memory"></param>
        /// <param name="cycles"></param>
        /// <returns></returns>
        private ushort ReadWord()
        {
            ushort data = ReadByte();
            data |= (ushort)(ReadByte() << 8);
            return data;
        }

        /// <summary>
        /// Reads a word from the specifiec address. Decreases cycles by two.
        /// </summary>
        /// <param name="memory"></param>
        /// <param name="cycles"></param>
        /// <returns></returns>
        private ushort ReadWord(ushort address)
        {
            ushort data = ReadByte(address);
            data |= (ushort)(ReadByte((ushort)(address + 1)) << 8);
            return data;
        }

        private void WriteByte(ushort address, byte data)
        {
            Bus.CpuWrite(address, data);
        }
        #endregion


        #region Load Store Operations

        private void LDA_IM()
        {
            A = ReadByte();
            CurrentInstruction = $"LDA_IM ${A:X}";
            SetNZFlags(A);
            Cycles += 2;
        }

        private void LDA_ZP()
        {
            var address = GetZeroPageAddress();
            A = ReadByte(address);
            CurrentInstruction = $"LDA_ZP ${A:X}";
            SetNZFlags(A);
            Cycles += 3;
        }

        private void LDA_ZPX()
        {
            var adr = GetZeroPageIndexedAddress(X);
            A = ReadByte(adr);
            CurrentInstruction = $"LDA_ZPX ${A:X}";
            SetNZFlags(A);
            Cycles += 4;
        }

        private void LDA_ABS()
        {
            var address = GetAbsolutAddress();
            A = ReadByte(address);
            CurrentInstruction = $"LDA_ABS ${A:X}";
            SetNZFlags(A);
            Cycles += 4;
        }

        private void LDA_ABS_X()
        {
            var address = GetAbsoluteIndexedAddress(X);
            A = ReadByte(address);
            CurrentInstruction = $"LDA_ABS_X ${A:X}";
            SetNZFlags(A);
            Cycles += 4;
        }

        private void LDA_ABS_Y()
        {
            var address = GetAbsoluteIndexedAddress(Y);
            A = ReadByte(address);
            CurrentInstruction = $"LDA_ABS_Y ${A:X}";
            SetNZFlags(A);
            Cycles += 4;
        }

        // Indexed Indirect Addressing
        private void LDA_IND_X()
        {
            var address = GetIndexedIndirectAddress();
            A = ReadByte(address);
            CurrentInstruction = $"LDA_IND_X ${A:X}";
            SetNZFlags(A);
            Cycles += 6;
        }

        // Indirect indexed addressing
        private void LDA_IND_Y()
        {
            var address = GetIndirectIndexedAddress();
            A = ReadByte(address);
            CurrentInstruction = $"LDA_IND_Y ${A:X}";
            SetNZFlags(A);
            Cycles += 5;
        }

        private void LDX_IM()
        {
            X = ReadByte();
            CurrentInstruction = $"LDX_IM ${X:X}";
            SetNZFlags(X);
            Cycles += 2;
        }

        private void LDX_ZP()
        {
            var address = GetZeroPageAddress();
            X = ReadByte(address);
            CurrentInstruction = $"LDX_ZP ${X:X}";
            SetNZFlags(X);
            Cycles += 3;
        }

        private void LDX_ZPY()
        {
            var address = GetZeroPageIndexedAddress(Y);
            X = ReadByte(address);
            CurrentInstruction = $"LDX_ZPY ${X:X}";
            SetNZFlags(X);
            Cycles += 4;
        }

        private void LDX_ABS()
        {
            var address = GetAbsolutAddress();
            X = ReadByte(address);
            CurrentInstruction = $"LDX_ABS ${X:X}";
            SetNZFlags(X);
            Cycles += 4;
        }

        private void LDX_ABS_Y()
        {
            var address = GetAbsoluteIndexedAddress(Y);
            X = ReadByte(address);
            CurrentInstruction = $"LDX_ABS_Y ${X:X}";
            SetNZFlags(X);
            Cycles += 4;
        }

        private void LDY_IM()
        {
            Y = ReadByte();
            CurrentInstruction = $"LDY_IM ${Y:X}";
            SetNZFlags(Y);
            Cycles += 2;
        }

        private void LDY_ZP()
        {
            var address = GetZeroPageAddress();
            Y = ReadByte(address);
            CurrentInstruction = $"LDY_ZP ${Y:X}"; ;
            SetNZFlags(Y);
            Cycles += 3;
        }

        private void LDY_ZPX()
        {
            var address = GetZeroPageIndexedAddress(X);
            Y = ReadByte(address);
            CurrentInstruction = $"LDY_ZPX ${Y:X}"; ;
            SetNZFlags(Y);
            Cycles += 4;
        }

        private void LDY_ABS()
        {
            var address = GetAbsolutAddress();
            Y = ReadByte(address);
            CurrentInstruction = $"LDY_ABS ${Y:X}"; ;
            SetNZFlags(Y);
            Cycles += 4;
        }

        private void LDY_ABS_X()
        {
            var address = GetAbsoluteIndexedAddress(X);
            Y = ReadByte(address);
            CurrentInstruction = $"LDY_ABS_X ${Y:X}"; ;
            SetNZFlags(Y);
            Cycles += 4;
        }

        private void STA_ZP()
        {
            var address = GetZeroPageAddress();
            WriteByte(address, A);
            CurrentInstruction = $"STA_ZP ${A:X}"; ;
            Cycles += 3;
        }

        private void STA_ZPX()
        {
            var address = GetZeroPageIndexedAddress(X);
            WriteByte(address, A);
            CurrentInstruction = $"STA_ZPX ${A:X}";
            Cycles += 4;
        }

        private void STA_ABS()
        {
            var address = GetAbsolutAddress();
            WriteByte(address, A);
            CurrentInstruction = $"STA_ABS ${address:X} ${A:X}";
            Cycles += 4;
        }

        private void STA_ABS_X()
        {
            var address = GetAbsoluteIndexedAddress(X);
            WriteByte(address, A);
            CurrentInstruction = $"STA_ABS_X ${A:X}";
            Cycles += 5;
        }

        private void STA_ABS_Y()
        {
            var address = GetAbsoluteIndexedAddress(Y);
            WriteByte(address, A);
            CurrentInstruction = $"STA_ABS_Y ${A:X}";
            Cycles += 5;
        }

        private void STA_IND_X()
        {
            var address = GetIndexedIndirectAddress();
            WriteByte(address, A);
            CurrentInstruction = $"STA_IND_X ${A:X}";
            Cycles += 6;
        }

        private void STA_IND_Y()
        {
            var address = GetIndirectIndexedAddress(true);
            WriteByte(address, A);
            CurrentInstruction = $"STA_IND_Y ${A:X}";
            Cycles += 6;
        }

        private void STX_ZP()
        {
            var address = GetZeroPageAddress();
            WriteByte(address, X);
            CurrentInstruction = $"STX_ZP ${X:X}";
            Cycles += 3;
        }

        private void STX_ZPY()
        {
            var address = GetZeroPageIndexedAddress(Y);
            WriteByte(address, X);
            CurrentInstruction = $"STX_ZPY ${X:X}";
            Cycles += 4;
        }

        private void STX_ABS()
        {
            var address = GetAbsolutAddress();
            WriteByte(address, X);
            CurrentInstruction = $"STX_ABS ${X:X}";
            Cycles += 4;
        }

        private void STY_ZP()
        {
            var address = GetZeroPageAddress();
            WriteByte(address, Y);
            CurrentInstruction = $"STY_ZP ${Y:X}";
            Cycles += 3;
        }

        private void STY_ZPX()
        {
            var address = GetZeroPageIndexedAddress(X);
            WriteByte(address, Y);
            CurrentInstruction = $"STY_ZPY ${Y:X}";
            Cycles += 4;
        }

        private void STY_ABS()
        {
            var address = GetAbsolutAddress();
            WriteByte(address, Y);
            CurrentInstruction = $"STY_ABS ${Y:X}";
            Cycles += 4;
        }

        private void LAX_ZP()
        {
            var address = GetZeroPageAddress();
            var value = ReadByte(address);
            A = value;
            X = A;
            SetNZFlags(A);
            CurrentInstruction = $"LAX_IM ${A:X}";
            Cycles += 3;
        }

        private void LAX_ZPY()
        {
            var address = GetZeroPageIndexedAddress(Y);
            var value = ReadByte(address);
            A = value;
            X = A;
            SetNZFlags(A);
            CurrentInstruction = $"LAX_ZPY ${A:X}";
            Cycles += 5;
        }

        private void LAX_ABS()
        {
            var address = GetAbsolutAddress();
            var value = ReadByte(address);
            A = value;
            X = value;
            SetNZFlags(A);
            CurrentInstruction = $"LAX_ABS ${A:X}";
            Cycles += 4;
        }

        private void LAX_ABS_Y()
        {
            var address = GetAbsoluteIndexedAddress(Y);
            var value = ReadByte(address);
            A = value;
            X = value;
            SetNZFlags(A);
            CurrentInstruction = $"LAX_ABS_Y ${A:X}";
            Cycles += 4;
        }

        private void LAX_IND_X()
        {
            var address = GetIndexedIndirectAddress();
            A = ReadByte(address);
            X = A;
            SetNZFlags(A);
            CurrentInstruction = $"LAX_IND_X ${A:X}";
            Cycles += 6;
        }

        private void LAX_IND_Y()
        {
            var address = GetIndirectIndexedAddress();
            A = ReadByte(address);
            X = A;
            SetNZFlags(A);
            CurrentInstruction = $"LAX_IND_Y ${A:X}";
            Cycles += 5;
        }

        private void SAX_IM()
        {
            var address = ReadByte();
            byte value = (byte)(A & X);
            WriteByte(address, value);
            CurrentInstruction = $"SAX_IM ${A:value}";
            Cycles += 3;
        }

        private void SAX_ZPY()
        {
            var address = GetZeroPageIndexedAddress(Y);
            byte value = (byte)(A & X);
            WriteByte(address, value);
            CurrentInstruction = $"SAX_ZPY ${A:value}";
            Cycles += 4;
        }

        private void SAX_ABS()
        {
            var address = GetAbsolutAddress();
            byte value = (byte)(A & X);
            WriteByte(address, value);
            CurrentInstruction = $"SAX_ABS ${A:value}";
            Cycles += 4;
        }

        private void SAX_IND_X()
        {
            var address = GetIndexedIndirectAddress();
            byte value = (byte)(A & X);
            WriteByte(address, value);
            CurrentInstruction = $"SAX_IND_X ${A:value}";
            Cycles += 6;
        }
        #endregion

        #region Register Transfers
        private void TAX()
        {
            X = A;
            SetNZFlags(X);
            Cycles += 2;
        }

        private void TAY()
        {
            Y = A;
            SetNZFlags(Y);
            Cycles += 2;
        }

        private void TXA()
        {
            A = X;
            SetNZFlags(A);
            Cycles += 2;
        }

        private void TYA()
        {
            A = Y;
            SetNZFlags(A);
            Cycles += 2;
        }
        #endregion

        #region Stack Operations
        private void TSX()
        {
            X = SP;
            SetNZFlags(X);
            Cycles += 2;
        }

        private void TXS()
        {
            SP = X;
            Cycles += 2;
        }

        private void PHA()
        {
            Push(A);
            Cycles += 3;
        }

        private void PHP()
        {
            var status = P.Get();
            status = (byte)(status | 0b10000);
            Push(status);
            Cycles += 3;
        }

        private void PLA()
        {
            A = Pop();
            SetNZFlags(A);
            Cycles += 4;
        }

        private void PLP()
        {
            var status = Pop();
            P.N = IsBitSet(status, 7);
            P.V = IsBitSet(status, 6);
            //P.U = IsBitSet(status, 5);
            //P.B = false;
            P.D = IsBitSet(status, 3);
            P.I = IsBitSet(status, 2);
            P.Z = IsBitSet(status, 1);
            P.C = IsBitSet(status, 0);
            Cycles += 4;
        }
        #endregion

        #region Logical
        private void AND_IM()
        {
            var value = ReadByte();
            A = (byte)(A & value);
            CurrentInstruction = $"AND_IM ${A:X}";
            SetNZFlags(A);
            Cycles += 2;
        }

        private void AND_ZP()
        {
            var address = GetZeroPageAddress();
            var value = ReadByte(address);
            A = (byte)(A & value);
            CurrentInstruction = $"AND_ZP ${A:X}";
            SetNZFlags(A);
            Cycles += 3;
        }

        private void AND_ZPX()
        {
            var address = GetZeroPageIndexedAddress(X);
            var value = ReadByte(address);
            A = (byte)(A & value);
            CurrentInstruction = $"AND_ZPX ${A:X}";
            SetNZFlags(A);
            Cycles += 4;
        }

        private void AND_ABS()
        {
            var address = GetAbsolutAddress();
            var value = ReadByte(address);
            A = (byte)(A & value);
            CurrentInstruction = $"AND_ABS ${A:X}";
            SetNZFlags(A);
            Cycles += 4;
        }

        private void AND_ABS_X()
        {
            var address = GetAbsoluteIndexedAddress(X);
            var value = ReadByte(address);
            A = (byte)(A & value);
            CurrentInstruction = $"AND_ABS_X ${A:X}";
            SetNZFlags(A);
            Cycles += 4;
        }

        private void AND_ABS_Y()
        {
            var address = GetAbsoluteIndexedAddress(Y);
            var value = ReadByte(address);
            A = (byte)(A & value);
            CurrentInstruction = $"AND_ABS_Y ${A:X}";
            SetNZFlags(A);
            Cycles += 4;
        }

        private void AND_IND_X()
        {
            var address = GetIndexedIndirectAddress();
            var value = ReadByte(address);
            A = (byte)(A & value);
            CurrentInstruction = $"AND_IND_X ${A:X}";
            SetNZFlags(A);
            Cycles += 6;
        }

        private void AND_IND_Y()
        {
            var address = GetIndirectIndexedAddress();
            var value = ReadByte(address);
            A = (byte)(A & value);
            CurrentInstruction = $"AND_IND_Y ${A:X}";
            SetNZFlags(A);
            Cycles += 5;
        }

        private void EOR_IM()
        {
            var value = ReadByte();
            A = (byte)(A ^ value);
            CurrentInstruction = $"EOR_IM ${A:X}";
            SetNZFlags(A);
            Cycles += 2;
        }

        private void EOR_ZP()
        {
            var address = GetZeroPageAddress();
            var value = ReadByte(address);
            A = (byte)(A ^ value);
            CurrentInstruction = $"EOR_ZP ${A:X}";
            SetNZFlags(A);
            Cycles += 3;
        }

        private void EOR_ZPX()
        {
            var address = GetZeroPageIndexedAddress(X);
            var value = ReadByte(address);
            A = (byte)(A ^ value);
            CurrentInstruction = $"EOR_ZPX ${A:X}";
            SetNZFlags(A);
            Cycles += 4;
        }

        private void EOR_ABS()
        {
            var address = GetAbsolutAddress();
            var value = ReadByte(address);
            A = (byte)(A ^ value);
            CurrentInstruction = $"EOR_ABS ${A:X}";
            SetNZFlags(A);
            Cycles += 4;
        }

        private void EOR_ABS_X()
        {
            var address = GetAbsoluteIndexedAddress(X);
            var value = ReadByte(address);
            A = (byte)(A ^ value);
            CurrentInstruction = $"EOR_ABS_X ${A:X}";
            SetNZFlags(A);
            Cycles += 4;
        }

        private void EOR_ABS_Y()
        {
            var address = GetAbsoluteIndexedAddress(Y);
            var value = ReadByte(address);
            A = (byte)(A ^ value);
            CurrentInstruction = $"EOR_ABS_Y ${A:X}";
            SetNZFlags(A);
            Cycles += 4;
        }

        private void EOR_IND_X()
        {
            var address = GetIndexedIndirectAddress();
            var value = ReadByte(address);
            A = (byte)(A ^ value);
            CurrentInstruction = $"EOR_IND_X ${A:X}";
            SetNZFlags(A);
            Cycles += 6;
        }

        private void EOR_IND_Y()
        {
            var address = GetIndirectIndexedAddress();
            var value = ReadByte(address);
            A = (byte)(A ^ value);
            CurrentInstruction = $"EOR_IND_Y ${A:X}";
            SetNZFlags(A);
            Cycles += 5;
        }

        private void ORA_IM()
        {
            var value = ReadByte();
            A = (byte)(A | value);
            CurrentInstruction = $"ORA_IM ${A:X}";
            SetNZFlags(A);
            Cycles += 2;
        }

        private void ORA_ZP()
        {
            var address = GetZeroPageAddress();
            var value = ReadByte(address);
            A = (byte)(A | value);
            CurrentInstruction = $"ORA_ZP ${A:X}";
            SetNZFlags(A);
            Cycles += 3;
        }

        private void ORA_ZPX()
        {
            var address = GetZeroPageIndexedAddress(X);
            var value = ReadByte(address);
            A = (byte)(A | value);
            CurrentInstruction = $"ORA_ZPX ${A:X}";
            SetNZFlags(A);
            Cycles += 4;
        }

        private void ORA_ABS()
        {
            var address = GetAbsolutAddress();
            var value = ReadByte(address);
            A = (byte)(A | value);
            CurrentInstruction = $"ORA_ABS ${A:X}";
            SetNZFlags(A);
            Cycles += 4;
        }

        private void ORA_ABS_X()
        {
            var address = GetAbsoluteIndexedAddress(X);
            var value = ReadByte(address);
            A = (byte)(A | value);
            CurrentInstruction = $"ORA_ABS_X ${A:X}";
            SetNZFlags(A);
            Cycles += 4;
        }

        private void ORA_ABS_Y()
        {
            var address = GetAbsoluteIndexedAddress(Y);
            var value = ReadByte(address);
            A = (byte)(A | value);
            CurrentInstruction = $"ORA_ABS_Y ${A:X}";
            SetNZFlags(A);
            Cycles += 4;
        }

        private void ORA_IND_X()
        {
            var address = GetIndexedIndirectAddress();
            var value = ReadByte(address);
            A = (byte)(A | value);
            CurrentInstruction = $"ORA_IND_X ${A:X}";
            SetNZFlags(A);
            Cycles += 6;
        }

        private void ORA_IND_Y()
        {
            var address = GetIndirectIndexedAddress();
            var value = ReadByte(address);
            A = (byte)(A | value);
            CurrentInstruction = $"ORA_IND_Y ${A:X}";
            SetNZFlags(A);
            Cycles += 5;
        }

        private void BIT_ZP()
        {
            var address = GetZeroPageAddress();
            var value = ReadByte(address);
            CurrentInstruction = $"BIT_ZP ${value:X}";
            P.Z = ((byte)(A & value)) == 0x0;
            P.V = IsBitSet(value, 6);
            P.N = IsBitSet(value, 7);
            Cycles += 3;
        }

        private void BIT_ABS()
        {
            var address = GetAbsolutAddress();
            var value = ReadByte(address);
            CurrentInstruction = $"BIT_ABS ${A:X}";
            P.Z = ((byte)(A & value)) == 0x0;
            P.V = IsBitSet(value, 6);
            P.N = IsBitSet(value, 7);
            Cycles += 4;
        }
        #endregion

        #region Aritmetic
        private void ADC_IM()
        {
            var value = ReadByte();
            var carry = (byte)(P.C ? 1 : 0);
            P.C = (byte)((A + value + carry) / 0x100) > 0;
            byte result = (byte)(A + value + carry);
            P.V = IsOverflow(A, value, result);
            A = result;
            CurrentInstruction = $"ADC_IM ${A:X}";
            SetNZFlags(A);
            Cycles += 2;
        }

        private void ADC_ZP()
        {
            var address = GetZeroPageAddress();
            var value = ReadByte(address);
            var carry = (byte)(P.C ? 1 : 0);
            P.C = (byte)((A + value + carry) / 0x100) > 0;
            byte result = (byte)(A + value + carry);
            P.V = IsOverflow(A, value, result);
            A = result;
            CurrentInstruction = $"ADC_ZP ${A:X}";
            SetNZFlags(A);
            Cycles += 3;
        }

        private void ADC_ZPX()
        {
            var address = GetZeroPageIndexedAddress(X);
            var value = ReadByte(address);
            var carry = (byte)(P.C ? 1 : 0);
            P.C = (byte)((A + value + carry) / 0x100) > 0;
            byte result = (byte)(A + value + carry);
            P.V = IsOverflow(A, value, result);
            A = result;
            CurrentInstruction = $"ADC_ZPX ${A:X}";
            SetNZFlags(A);
            Cycles += 4;
        }

        private void ADC_ABS()
        {
            var address = GetAbsolutAddress();
            var value = ReadByte(address);
            var carry = (byte)(P.C ? 1 : 0);
            P.C = (byte)((A + value + carry) / 0x100) > 0;
            byte result = (byte)(A + value + carry);
            P.V = IsOverflow(A, value, result);
            A = result;
            CurrentInstruction = $"ADC_ABS ${A:X}";
            SetNZFlags(A);
            Cycles += 4;
        }

        private void ADC_ABS_X()
        {
            var address = GetAbsoluteIndexedAddress(X);
            var value = ReadByte(address);
            var carry = (byte)(P.C ? 1 : 0);
            P.C = (byte)((A + value + carry) / 0x100) > 0;
            byte result = (byte)(A + value + carry);
            P.V = IsOverflow(A, value, result);
            A = result;
            CurrentInstruction = $"ADC_ABS_X ${A:X}";
            SetNZFlags(A);
            Cycles += 4;
        }

        private void ADC_ABS_Y()
        {
            var address = GetAbsoluteIndexedAddress(Y);
            var value = ReadByte(address);
            var carry = (byte)(P.C ? 1 : 0);
            P.C = (byte)((A + value + carry) / 0x100) > 0;
            byte result = (byte)(A + value + carry);
            P.V = IsOverflow(A, value, result);
            A = result;
            CurrentInstruction = $"ADC_ABS_Y ${A:X}";
            SetNZFlags(A);
            Cycles += 4;
        }

        private void ADC_IND_X()
        {
            var address = GetIndexedIndirectAddress();
            var value = ReadByte(address);
            var carry = (byte)(P.C ? 1 : 0);
            P.C = (byte)((A + value + carry) / 0x100) > 0;
            byte result = (byte)(A + value + carry);
            P.V = IsOverflow(A, value, result);
            A = result;
            CurrentInstruction = $"ADC_IND_X ${A:X}";
            SetNZFlags(A);
            Cycles += 6;
        }

        private void ADC_IND_Y()
        {
            var address = GetIndirectIndexedAddress();
            var value = ReadByte(address);
            var carry = (byte)(P.C ? 1 : 0);
            P.C = (byte)((A + value + carry) / 0x100) > 0;
            byte result = (byte)(A + value + carry);
            P.V = IsOverflow(A, value, result);
            A = result;
            CurrentInstruction = $"ADC_IND_Y ${A:X}";
            SetNZFlags(A);
            Cycles += 5;
        }

        private void SBC_IM()
        {
            var value = ReadByte();
            value = (byte)(value ^ 0xFF);
            var carry = (byte)(P.C ? 1 : 0);
            byte result = (byte)(A + value + carry);
            P.C = (byte)((A + value + carry) / 0x100) > 0;
            P.V = IsOverflow(A, value, result);
            A = result;
            CurrentInstruction = $"SBC_IM ${A:X}";
            SetNZFlags(A);
            Cycles += 2;
        }

        private void SBC_ZP()
        {
            var address = GetZeroPageAddress();
            var value = ReadByte(address);
            value = (byte)(value ^ 0xFF);
            var carry = (byte)(P.C ? 1 : 0);
            byte result = (byte)(A + value + carry);
            P.C = (byte)((A + value + carry) / 0x100) > 0;
            P.V = IsOverflow(A, value, result);
            A = result;
            CurrentInstruction = $"SBC_ZP ${A:X}";
            SetNZFlags(A);
            Cycles += 3;
        }

        private void SBC_ZPX()
        {
            var address = GetZeroPageIndexedAddress(X);
            var value = ReadByte(address);
            value = (byte)(value ^ 0xFF);
            var carry = (byte)(P.C ? 1 : 0);
            byte result = (byte)(A + value + carry);
            P.C = (byte)((A + value + carry) / 0x100) > 0;
            P.V = IsOverflow(A, value, result);
            A = result;
            CurrentInstruction = $"SBC_ZPX ${A:X}";
            SetNZFlags(A);
            Cycles += 4;
        }

        private void SBC_ABS()
        {
            var address = GetAbsolutAddress();
            var value = ReadByte(address);
            value = (byte)(value ^ 0xFF);
            var carry = (byte)(P.C ? 1 : 0);
            byte result = (byte)(A + value + carry);
            P.C = (byte)((A + value + carry) / 0x100) > 0;
            P.V = IsOverflow(A, value, result);
            A = result;
            CurrentInstruction = $"SBC_ABS ${A:X}";
            SetNZFlags(A);
            Cycles += 4;
        }

        private void SBC_ABS_X()
        {
            var address = GetAbsoluteIndexedAddress(X);
            var value = ReadByte(address);
            value = (byte)(value ^ 0xFF);
            var carry = (byte)(P.C ? 1 : 0);
            byte result = (byte)(A + value + carry);
            P.C = (byte)((A + value + carry) / 0x100) > 0;
            P.V = IsOverflow(A, value, result);
            A = result;
            CurrentInstruction = $"SBC_ABS_X ${A:X}";
            SetNZFlags(A);
        }

        private void SBC_ABS_Y()
        {
            var address = GetAbsoluteIndexedAddress(Y);
            var value = ReadByte(address);
            value = (byte)(value ^ 0xFF);
            var carry = (byte)(P.C ? 1 : 0);
            byte result = (byte)(A + value + carry);
            P.C = (byte)((A + value + carry) / 0x100) > 0;
            P.V = IsOverflow(A, value, result);
            A = result;
            CurrentInstruction = $"SBC_ABS_Y ${A:X}";
            SetNZFlags(A);
            Cycles += 4;
        }

        private void SBC_IND_X()
        {
            var address = GetIndexedIndirectAddress();
            var value = ReadByte(address);
            value = (byte)(value ^ 0xFF);
            var carry = (byte)(P.C ? 1 : 0);
            byte result = (byte)(A + value + carry);
            P.C = (byte)((A + value + carry) / 0x100) > 0;
            P.V = IsOverflow(A, value, result);
            A = result;
            CurrentInstruction = $"SBC_IND_X ${A:X}";
            SetNZFlags(A);
            Cycles += 6;
        }

        private void SBC_IND_Y()
        {
            var address = GetIndirectIndexedAddress();
            var value = ReadByte(address);
            value = (byte)(value ^ 0xFF);
            var carry = (byte)(P.C ? 1 : 0);
            byte result = (byte)(A + value + carry);
            P.C = (byte)((A + value + carry) / 0x100) > 0;
            P.V = IsOverflow(A, value, result);
            A = result;
            CurrentInstruction = $"SBC_IND_Y ${A:X}";
            SetNZFlags(A);
            Cycles += 5;
        }

        private void CMP_IM()
        {
            var value = ReadByte();
            P.C = A >= value;
            P.Z = A == value;
            var result = (byte)(A - value);
            P.N = IsBitSet(result, 7);
            CurrentInstruction = $"CMP_IM";
            Cycles += 2;
        }

        private void CMP_ZP()
        {
            var address = GetZeroPageAddress();
            var value = ReadByte(address);
            P.C = A >= value;
            P.Z = A == value;
            var result = (byte)(A - value);
            P.N = IsBitSet(result, 7);
            CurrentInstruction = $"CMP_ZP";
            Cycles += 3;
        }

        private void CMP_ZPX()
        {
            var address = GetZeroPageIndexedAddress(X);
            var value = ReadByte(address);
            P.C = A >= value;
            P.Z = A == value;
            var result = (byte)(A - value);
            P.N = IsBitSet(result, 7);
            CurrentInstruction = $"CMP_ZPX";
            Cycles += 4;
        }

        private void CMP_ABS()
        {
            var address = GetAbsolutAddress();
            var value = ReadByte(address);
            P.C = A >= value;
            P.Z = A == value;
            var result = (byte)(A - value);
            P.N = IsBitSet(result, 7);
            CurrentInstruction = $"CMP_ABS";
            Cycles += 4;
        }

        private void CMP_ABS_X()
        {
            var address = GetAbsoluteIndexedAddress(X);
            var value = ReadByte(address);
            P.C = A >= value;
            P.Z = A == value;
            var result = (byte)(A - value);
            P.N = IsBitSet(result, 7);
            CurrentInstruction = $"CMP_ABS_X";
            Cycles += 4;
        }

        private void CMP_ABS_Y()
        {
            var address = GetAbsoluteIndexedAddress(Y);
            var value = ReadByte(address);
            P.C = A >= value;
            P.Z = A == value;
            var result = (byte)(A - value);
            P.N = IsBitSet(result, 7);
            CurrentInstruction = $"CMP_ABS_Y";
            Cycles += 4;
        }

        private void CMP_IND_X()
        {
            var address = GetIndexedIndirectAddress();
            var value = ReadByte(address);
            P.C = A >= value;
            P.Z = A == value;
            var result = (byte)(A - value);
            P.N = IsBitSet(result, 7);
            CurrentInstruction = $"CMP_IND_X";
            Cycles += 6;
        }

        private void CMP_IND_Y()
        {
            var address = GetIndirectIndexedAddress();
            var value = ReadByte(address);
            P.C = A >= value;
            P.Z = A == value;
            var result = (byte)(A - value);
            P.N = IsBitSet(result, 7);
            CurrentInstruction = $"CMP_IND_Y";
            Cycles += 5;
        }

        private void CPX_IM()
        {
            var value = ReadByte();
            P.C = X >= value;
            P.Z = X == value;
            var result = (byte)(X - value);
            P.N = IsBitSet(result, 7);
            CurrentInstruction = $"CPX_IM";
            Cycles += 2;
        }

        private void CPX_ZP()
        {
            var address = GetZeroPageAddress();
            var value = ReadByte(address);
            P.C = X >= value;
            P.Z = X == value;
            var result = (byte)(X - value);
            P.N = IsBitSet(result, 7);
            CurrentInstruction = $"CPX_ZP";
            Cycles += 3;
        }

        private void CPX_ABS()
        {
            var address = GetAbsolutAddress();
            var value = ReadByte(address);
            P.C = X >= value;
            P.Z = X == value;
            var result = (byte)(X - value);
            P.N = IsBitSet(result, 7);
            CurrentInstruction = $"CPX_ABS";
            Cycles += 4;
        }

        private void CPY_IM()
        {
            var value = ReadByte();
            P.C = Y >= value;
            P.Z = Y == value;
            var result = (byte)(Y - value);
            P.N = IsBitSet(result, 7);
            CurrentInstruction = $"CPY_IM";
            Cycles += 2;
        }

        private void CPY_ZP()
        {
            var address = GetZeroPageAddress();
            var value = ReadByte(address);
            P.C = Y >= value;
            P.Z = Y == value;
            var result = (byte)(Y - value);
            P.N = IsBitSet(result, 7);
            CurrentInstruction = $"CPY_ZP";
            Cycles += 3;
        }

        private void CPY_ABS()
        {
            var address = GetAbsolutAddress();
            var value = ReadByte(address);
            P.C = Y >= value;
            P.Z = Y == value;
            var result = (byte)(Y - value);
            P.N = IsBitSet(result, 7);
            CurrentInstruction = $"CPY_ABS";
            Cycles += 4;
        }

        private void DCP_ZP()
        {
            var address = GetZeroPageAddress();
            var value = ReadByte(address);
            value = (byte)(value - 1);
            WriteByte(address, value);
            P.C = A >= value;
            P.Z = A == value;
            var result = (byte)(A - value);
            P.N = IsBitSet(result, 7);
            CurrentInstruction = $"DCP_ZP ${value:X}";
            Cycles += 5;
        }
        private void DCP_ZP_X()
        {
            var address = GetZeroPageIndexedAddress(X);
            var value = ReadByte(address);
            value = (byte)(value - 1);
            WriteByte(address, value);
            P.C = A >= value;
            P.Z = A == value;
            var result = (byte)(A - value);
            P.N = IsBitSet(result, 7);
            CurrentInstruction = $"DCP_ZP_X ${value:X}";
            Cycles += 6;
        }

        private void DCP_ABS()
        {
            var address = GetAbsolutAddress();
            var value = ReadByte(address);
            value = (byte)(value - 1);
            WriteByte(address, value);
            P.C = A >= value;
            P.Z = A == value;
            var result = (byte)(A - value);
            P.N = IsBitSet(result, 7);
            CurrentInstruction = $"DCP_ZP ${value:X}";
            Cycles += 6;
        }

        private void DCP_ABS_Y()
        {
            var address = GetAbsoluteIndexedAddress(Y);
            var value = ReadByte(address);
            value = (byte)(value - 1);
            WriteByte(address, value);
            P.C = A >= value;
            P.Z = A == value;
            var result = (byte)(A - value);
            P.N = IsBitSet(result, 7);
            CurrentInstruction = $"DCP_ZP ${value:X}";
            Cycles += 7;
        }

        private void DCP_ABS_X()
        {
            var address = GetAbsoluteIndexedAddress(X);
            var value = ReadByte(address);
            value = (byte)(value - 1);
            WriteByte(address, value);
            P.C = A >= value;
            P.Z = A == value;
            var result = (byte)(A - value);
            P.N = IsBitSet(result, 7);
            CurrentInstruction = $"DCP_ZP ${value:X}";
            Cycles += 7;
        }

        private void DCP_IND_X()
        {
            var address = GetIndexedIndirectAddress();
            var value = ReadByte(address);
            value = (byte)(value - 1);
            WriteByte(address, value);
            P.C = A >= value;
            P.Z = A == value;
            var result = (byte)(A - value);
            P.N = IsBitSet(result, 7);
            CurrentInstruction = $"DCP_IND_X ${value:X}";
            Cycles += 6;
        }
        private void DCP_IND_Y()
        {
            var address = GetIndirectIndexedAddress();
            var value = ReadByte(address);
            value = (byte)(value - 1);
            WriteByte(address, value);
            P.C = A >= value;
            P.Z = A == value;
            var result = (byte)(A - value);
            P.N = IsBitSet(result, 7);
            CurrentInstruction = $"DCP_IND_X ${value:X}";
            Cycles += 8;
        }
        #endregion

        #region Increments & Decrements
        private void INC_ZP()
        {
            var address = GetZeroPageAddress();
            var value = ReadByte(address);
            value = (byte)(value + 1);
            WriteByte(address, value);
            SetNZFlags(value);
            CurrentInstruction = $"INC_ZP ${value:X}";
            Cycles += 5;
        }

        private void INC_ZPX()
        {
            var address = GetZeroPageIndexedAddress(X);
            var value = ReadByte(address);
            value = (byte)(value + 1);
            WriteByte(address, value);
            SetNZFlags(value);
            CurrentInstruction = $"INC_ZPX ${value:X}";
            Cycles += 6;
        }

        private void INC_ABS()
        {
            var address = GetAbsolutAddress();
            var value = ReadByte(address);
            value = (byte)(value + 1);
            WriteByte(address, value);
            SetNZFlags(value);
            CurrentInstruction = $"INC_ABS ${value:X}";
            Cycles += 6;
        }

        private void INC_ABS_X()
        {
            var address = GetAbsoluteIndexedAddress(X);
            var value = ReadByte(address);
            value = (byte)(value + 1);
            WriteByte(address, value);
            SetNZFlags(value);
            CurrentInstruction = $"INC_ABS_X ${value:X}";
            Cycles += 7;
        }

        private void ISB_ZP() 
        {
            var address = GetZeroPageAddress();
            var value = ReadByte(address);
            value = (byte)(value + 1);
            WriteByte(address, value);
            value = (byte)(value ^ 0xFF);
            var carry = (byte)(P.C ? 1 : 0);
            byte result = (byte)(A + value + carry);
            P.C = (byte)((A + value + carry) / 0x100) > 0;
            P.V = IsOverflow(A, value, result);
            A = result;
            CurrentInstruction = $"ISB_ZP ${A:X}";
            SetNZFlags(A);
            Cycles += 5;
        }

        private void ISB_ZPX() 
        {
            var address = GetZeroPageIndexedAddress(X);
            var value = ReadByte(address);
            value = (byte)(value + 1);
            WriteByte(address, value);
            value = (byte)(value ^ 0xFF);
            var carry = (byte)(P.C ? 1 : 0);
            byte result = (byte)(A + value + carry);
            P.C = (byte)((A + value + carry) / 0x100) > 0;
            P.V = IsOverflow(A, value, result);
            A = result;
            CurrentInstruction = $"ISB_ZPX ${A:X}";
            SetNZFlags(A);
            Cycles += 6;
        }

        private void ISB_ABS() 
        {
            var address = GetAbsolutAddress();
            var value = ReadByte(address);
            value = (byte)(value + 1);
            WriteByte(address, value);
            value = (byte)(value ^ 0xFF);
            var carry = (byte)(P.C ? 1 : 0);
            byte result = (byte)(A + value + carry);
            P.C = (byte)((A + value + carry) / 0x100) > 0;
            P.V = IsOverflow(A, value, result);
            A = result;
            CurrentInstruction = $"ISB_ABS ${A:X}";
            SetNZFlags(A);
            Cycles += 6;
        }

        private void ISB_ABS_X() 
        {
            var address = GetAbsoluteIndexedAddress(X);
            var value = ReadByte(address);
            value = (byte)(value + 1);
            WriteByte(address, value);
            value = (byte)(value ^ 0xFF);
            var carry = (byte)(P.C ? 1 : 0);
            byte result = (byte)(A + value + carry);
            P.C = (byte)((A + value + carry) / 0x100) > 0;
            P.V = IsOverflow(A, value, result);
            A = result;
            CurrentInstruction = $"ISB_ABS_X ${A:X}";
            SetNZFlags(A);
            Cycles += 7;
        }

        private void ISB_ABS_Y() 
        {
            var address = GetAbsoluteIndexedAddress(Y);
            var value = ReadByte(address);
            value = (byte)(value + 1);
            WriteByte(address, value);
            value = (byte)(value ^ 0xFF);
            var carry = (byte)(P.C ? 1 : 0);
            byte result = (byte)(A + value + carry);
            P.C = (byte)((A + value + carry) / 0x100) > 0;
            P.V = IsOverflow(A, value, result);
            A = result;
            CurrentInstruction = $"ISB_ABS_Y ${A:X}";
            SetNZFlags(A);
            Cycles += 7;
        }

        private void ISB_IND_X() 
        {
            var address = GetIndexedIndirectAddress();
            var value = ReadByte(address);
            value = (byte)(value + 1);
            WriteByte(address, value);
            value = (byte)(value ^ 0xFF);
            var carry = (byte)(P.C ? 1 : 0);
            byte result = (byte)(A + value + carry);
            P.C = (byte)((A + value + carry) / 0x100) > 0;
            P.V = IsOverflow(A, value, result);
            A = result;
            CurrentInstruction = $"ISB_IND_X ${A:X}";
            SetNZFlags(A);
            Cycles += 8;
        }

        private void ISB_IND_Y() 
        {
            var address = GetIndirectIndexedAddress();
            var value = ReadByte(address);
            value = (byte)(value + 1);
            WriteByte(address, value);
            value = (byte)(value ^ 0xFF);
            var carry = (byte)(P.C ? 1 : 0);
            byte result = (byte)(A + value + carry);
            P.C = (byte)((A + value + carry) / 0x100) > 0;
            P.V = IsOverflow(A, value, result);
            A = result;
            CurrentInstruction = $"ISB_IND_Y ${A:X}";
            SetNZFlags(A);
            Cycles += 8;
        }

        private void INX()
        {
            X = (byte)(X + 1);
            SetNZFlags(X);
            CurrentInstruction = "INX";
            Cycles += 2;
        }

        private void INY()
        {
            Y = (byte)(Y + 1);
            SetNZFlags(Y);
            CurrentInstruction = "INY";
            Cycles += 2;
        }

        private void DEC_ZP()
        {
            var address = GetZeroPageAddress();
            var value = ReadByte(address);
            value = (byte)(value - 1);
            WriteByte(address, value);
            SetNZFlags(value);
            CurrentInstruction = $"DEC_ZP ${value:X}";
            Cycles += 5;
        }

        private void DEC_ZPX()
        {
            var address = GetZeroPageIndexedAddress(X);
            var value = ReadByte(address);
            value = (byte)(value - 1);
            WriteByte(address, value);
            SetNZFlags(value);
            CurrentInstruction = $"DEC_ZPX ${value:X}";
            Cycles += 6;
        }

        private void DEC_ABS()
        {
            var address = GetAbsolutAddress();
            var value = ReadByte(address);
            value = (byte)(value - 1);
            WriteByte(address, value);
            SetNZFlags(value);
            CurrentInstruction = $"DEC_ABS ${value:X}";
            Cycles += 6;
        }

        private void DEC_ABS_X()
        {
            var address = GetAbsoluteIndexedAddress(X);
            var value = ReadByte(address);
            value = (byte)(value - 1);
            WriteByte(address, value);
            SetNZFlags(value);
            CurrentInstruction = $"DEC_ABS_X ${value:X}";
            Cycles += 7;
        }

        private void DEX()
        {
            X = (byte)(X - 1);
            SetNZFlags(X);
            CurrentInstruction = $"DEX ${X:X}";
            Cycles += 2;
        }

        private void DEY()
        {
            Y = (byte)(Y - 1);
            SetNZFlags(Y);
            CurrentInstruction = $"DEY ${Y:X}";
            Cycles += 2;
        }
        #endregion

        #region Shifts
        private void ASL_ACC()
        {
            P.C = IsBitSet(A, 7);
            A = (byte)(A << 1);
            SetNZFlags(A);
            CurrentInstruction = $"ASL_ACC ${A:X}";
            Cycles += 2;
        }

        private void ASL_ZP()
        {
            var address = GetZeroPageAddress();
            var value = ReadByte(address);
            P.C = IsBitSet(value, 7);
            value = (byte)(value << 1);
            WriteByte(address, value);
            SetNZFlags(value);
            CurrentInstruction = $"ASL_ZP ${value:X}";
            Cycles += 5;
        }

        private void ASL_ZPX()
        {
            var address = GetZeroPageIndexedAddress(X);
            var value = ReadByte(address);
            P.C = IsBitSet(value, 7);
            value = (byte)(value << 1);
            WriteByte(address, value);
            SetNZFlags(value);
            CurrentInstruction = $"ASL_ZPX ${value:X}";
            Cycles += 6;
        }

        private void ASL_ABS()
        {
            var address = GetAbsolutAddress();
            var value = ReadByte(address);
            P.C = IsBitSet(value, 7);
            value = (byte)(value << 1);
            WriteByte(address, value);
            SetNZFlags(value);
            CurrentInstruction = $"ASL_ABS ${value:X}";
            Cycles += 6;
        }

        private void ASL_ABS_X()
        {
            var address = GetAbsoluteIndexedAddress(X);
            var value = ReadByte(address);
            P.C = IsBitSet(value, 7);
            value = (byte)(value << 1);
            WriteByte(address, value);
            SetNZFlags(value);
            CurrentInstruction = $"ASL_ABS_X ${value:X}";
            Cycles += 7;
        }

        private void SLO_ZP() 
        {
            var address = GetZeroPageAddress();
            var value = ReadByte(address);
            P.C = IsBitSet(value, 7);
            value = (byte)(value << 1);
            WriteByte(address, value);
            A = (byte)(A | value);
            CurrentInstruction = $"SLO_ZP ${A:X}";
            SetNZFlags(A);
            Cycles += 5;
        }

        private void SLO_ZPX() 
        {
            var address = GetZeroPageIndexedAddress(X);
            var value = ReadByte(address);
            P.C = IsBitSet(value, 7);
            value = (byte)(value << 1);
            WriteByte(address, value);
            A = (byte)(A | value);
            CurrentInstruction = $"SLO_ZPX ${A:X}";
            SetNZFlags(A);
            Cycles += 6;
        }

        private void SLO_ABS() 
        {
            var address = GetAbsolutAddress();
            var value = ReadByte(address);
            P.C = IsBitSet(value, 7);
            value = (byte)(value << 1);
            WriteByte(address, value);
            A = (byte)(A | value);
            CurrentInstruction = $"SLO_ABS ${A:X}";
            SetNZFlags(A);
            Cycles += 6;
        }

        private void SLO_ABS_X() 
        {
            var address = GetAbsoluteIndexedAddress(X);
            var value = ReadByte(address);
            P.C = IsBitSet(value, 7);
            value = (byte)(value << 1);
            WriteByte(address, value);
            A = (byte)(A | value);
            CurrentInstruction = $"SLO_ABS_X ${A:X}";
            SetNZFlags(A);
            Cycles += 7;
        }

        private void SLO_ABS_Y() 
        {
            var address = GetAbsoluteIndexedAddress(Y);
            var value = ReadByte(address);
            P.C = IsBitSet(value, 7);
            value = (byte)(value << 1);
            WriteByte(address, value);
            A = (byte)(A | value);
            CurrentInstruction = $"SLO_ABS_Y ${A:X}";
            SetNZFlags(A);
            Cycles += 7;
        }

        private void SLO_IND_X() 
        {
            var address = GetIndexedIndirectAddress();
            var value = ReadByte(address);
            P.C = IsBitSet(value, 7);
            value = (byte)(value << 1);
            WriteByte(address, value);
            A = (byte)(A | value);
            CurrentInstruction = $"SLO_IND_X ${A:X}";
            SetNZFlags(A);
            Cycles += 8;
        }

        private void SLO_IND_Y() 
        {
            var address = GetIndirectIndexedAddress();
            var value = ReadByte(address);
            P.C = IsBitSet(value, 7);
            value = (byte)(value << 1);
            WriteByte(address, value);
            A = (byte)(A | value);
            CurrentInstruction = $"SLO_IND_Y ${A:X}";
            SetNZFlags(A);
            Cycles += 8;
        }

        private void LSR_ACC()
        {
            P.C = IsBitSet(A, 0);
            A = (byte)(A >> 1);
            SetNZFlags(A);
            CurrentInstruction = $"LSR_ACC ${A:X}";
            Cycles += 2;
        }

        private void LSR_ZP()
        {
            var address = GetZeroPageAddress();
            var value = ReadByte(address);
            P.C = IsBitSet(value, 0);
            value = (byte)(value >> 1);
            WriteByte(address, value);
            SetNZFlags(value);
            CurrentInstruction = $"LSR_ZP ${value:X}";
            Cycles += 5;
        }

        private void LSR_ZPX()
        {
            var address = GetZeroPageIndexedAddress(X);
            var value = ReadByte(address);
            P.C = IsBitSet(value, 0);
            value = (byte)(value >> 1);
            WriteByte(address, value);
            SetNZFlags(value);
            CurrentInstruction = $"LSR_ZPX ${value:X}";
            Cycles += 6;
        }

        private void LSR_ABS()
        {
            var address = GetAbsolutAddress();
            var value = ReadByte(address);
            P.C = IsBitSet(value, 0);
            value = (byte)(value >> 1);
            WriteByte(address, value);
            SetNZFlags(value);
            CurrentInstruction = $"LSR_ABS ${value:X}";
            Cycles += 6;
        }

        private void LSR_ABS_X()
        {
            var address = GetAbsoluteIndexedAddress(X);
            var value = ReadByte(address);
            P.C = IsBitSet(value, 0);
            value = (byte)(value >> 1);
            WriteByte(address, value);
            SetNZFlags(value);
            CurrentInstruction = $"LSR_ABS_X ${value:X}";
            Cycles += 7;
        }

        private void ROL_ACC()
        {
            var value = A;
            bool carry = IsBitSet(value, 7);
            value = (byte)(value << 1);
            value = (byte)(value | (P.C ? 1 : 0));
            P.C = carry;
            A = value;
            SetNZFlags(A);
            CurrentInstruction = $"ROL_ACC ${A:X}";
            Cycles += 2;
        }

        private void ROL_ZP()
        {
            var address = GetZeroPageAddress();
            var value = ReadByte(address);
            bool carry = IsBitSet(value, 7);
            value = (byte)(value << 1);
            value = (byte)(value | (P.C ? 1 : 0));
            P.C = carry;
            WriteByte(address, value);
            SetNZFlags(value);
            CurrentInstruction = $"ROL_ZP ${value:X}";
            Cycles += 5;
        }

        private void ROL_ZPX()
        {
            var address = GetZeroPageIndexedAddress(X);
            var value = ReadByte(address);
            bool carry = IsBitSet(value, 7);
            value = (byte)(value << 1);
            value = (byte)(value | (P.C ? 1 : 0));
            P.C = carry;
            WriteByte(address, value);
            SetNZFlags(value);
            CurrentInstruction = $"ROL_ZPX ${value:X}";
            Cycles += 6;
        }

        private void ROL_ABS()
        {
            var address = GetAbsolutAddress();
            var value = ReadByte(address);
            bool carry = IsBitSet(value, 7);
            value = (byte)(value << 1);
            value = (byte)(value | (P.C ? 1 : 0));
            P.C = carry;
            WriteByte(address, value);
            SetNZFlags(value);
            CurrentInstruction = $"ROL_ABS ${value:X}";
            Cycles += 6;
        }

        private void RLA_ZP() 
        {
            var address = GetZeroPageAddress();
            var value = ReadByte(address);
            bool carry = IsBitSet(value, 7);
            value = (byte)(value << 1);
            value = (byte)(value | (P.C ? 1 : 0));
            P.C = carry;
            WriteByte(address, value);
            A = (byte)(A & value);
            CurrentInstruction = $"RLA_ZP ${A:X}";
            SetNZFlags(A);
            Cycles += 5;
        }

        private void RLA_ZPX() 
        {
            var address = GetZeroPageIndexedAddress(X);
            var value = ReadByte(address);
            bool carry = IsBitSet(value, 7);
            value = (byte)(value << 1);
            value = (byte)(value | (P.C ? 1 : 0));
            P.C = carry;
            WriteByte(address, value);
            A = (byte)(A & value);
            CurrentInstruction = $"RLA_ZPX ${A:X}";
            SetNZFlags(A);
            Cycles += 6;
        }

        private void RLA_ABS() 
        {
            var address = GetAbsolutAddress();
            var value = ReadByte(address);
            bool carry = IsBitSet(value, 7);
            value = (byte)(value << 1);
            value = (byte)(value | (P.C ? 1 : 0));
            P.C = carry;
            WriteByte(address, value);
            A = (byte)(A & value);
            CurrentInstruction = $"RLA_ABS ${A:X}";
            SetNZFlags(A);
            Cycles += 6;
        }

        private void RLA_ABS_X() 
        {
            var address = GetAbsoluteIndexedAddress(X);
            var value = ReadByte(address);
            bool carry = IsBitSet(value, 7);
            value = (byte)(value << 1);
            value = (byte)(value | (P.C ? 1 : 0));
            P.C = carry;
            WriteByte(address, value);
            A = (byte)(A & value);
            CurrentInstruction = $"RLA_ABS_X ${A:X}";
            SetNZFlags(A);
            Cycles += 7;
        }

        private void RLA_ABS_Y() 
        {
            var address = GetAbsoluteIndexedAddress(Y);
            var value = ReadByte(address);
            bool carry = IsBitSet(value, 7);
            value = (byte)(value << 1);
            value = (byte)(value | (P.C ? 1 : 0));
            P.C = carry;
            WriteByte(address, value);
            A = (byte)(A & value);
            CurrentInstruction = $"RLA_ABS_Y ${A:X}";
            SetNZFlags(A);
            Cycles += 7;
        }

        private void RLA_IND_X() 
        {
            var address = GetIndexedIndirectAddress();
            var value = ReadByte(address);
            bool carry = IsBitSet(value, 7);
            value = (byte)(value << 1);
            value = (byte)(value | (P.C ? 1 : 0));
            P.C = carry;
            WriteByte(address, value);
            A = (byte)(A & value);
            CurrentInstruction = $"RLA_IND_X ${A:X}";
            SetNZFlags(A);
            Cycles += 8;
        }

        private void RLA_IND_Y() 
        {
            var address = GetIndirectIndexedAddress();
            var value = ReadByte(address);
            bool carry = IsBitSet(value, 7);
            value = (byte)(value << 1);
            value = (byte)(value | (P.C ? 1 : 0));
            P.C = carry;
            WriteByte(address, value);
            A = (byte)(A & value);
            CurrentInstruction = $"RLA_IND_Y ${A:X}";
            SetNZFlags(A);
            Cycles += 8;
        }

        private void SRE_ZP() 
        {
            var address = GetZeroPageAddress();
            var value = ReadByte(address);
            bool carry = IsBitSet(value, 0);
            value = (byte)(value >> 1);
            P.C = carry;
            WriteByte(address, value);
            A = (byte)(A ^ value);
            SetNZFlags(A);
            CurrentInstruction = $"SRE_ZP ${A:X}";
            Cycles += 5;
        }

        private void SRE_ZPX() 
        {
            var address = GetZeroPageIndexedAddress(X);
            var value = ReadByte(address);
            bool carry = IsBitSet(value, 0);
            value = (byte)(value >> 1);
            P.C = carry;
            WriteByte(address, value);
            A = (byte)(A ^ value);
            SetNZFlags(A);
            CurrentInstruction = $"SRE_ZPX ${A:X}";
            Cycles += 6;
        }

        private void SRE_ABS() 
        {
            var address = GetAbsolutAddress();
            var value = ReadByte(address);
            bool carry = IsBitSet(value, 0);
            value = (byte)(value >> 1);
            P.C = carry;
            WriteByte(address, value);
            A = (byte)(A ^ value);
            SetNZFlags(A);
            CurrentInstruction = $"SRE_ABS ${A:X}";
            Cycles += 6;
        }

        private void SRE_ABS_X() 
        {
            var address = GetAbsoluteIndexedAddress(X);
            var value = ReadByte(address);
            bool carry = IsBitSet(value, 0);
            value = (byte)(value >> 1);
            P.C = carry;
            WriteByte(address, value);
            A = (byte)(A ^ value);
            SetNZFlags(A);
            CurrentInstruction = $"SRE_ABS_X ${A:X}";
            Cycles += 7;
        }

        private void SRE_ABS_Y() 
        {
            var address = GetAbsoluteIndexedAddress(Y);
            var value = ReadByte(address);
            bool carry = IsBitSet(value, 0);
            value = (byte)(value >> 1);
            P.C = carry;
            WriteByte(address, value);
            A = (byte)(A ^ value);
            SetNZFlags(A);
            CurrentInstruction = $"SRE_ABS_Y ${A:X}";
            Cycles += 7;
        }

        private void SRE_IND_X() 
        {
            var address = GetIndexedIndirectAddress();
            var value = ReadByte(address);
            bool carry = IsBitSet(value, 0);
            value = (byte)(value >> 1);
            P.C = carry;
            WriteByte(address, value);
            A = (byte)(A ^ value);
            SetNZFlags(A);
            CurrentInstruction = $"SRE_IND_X ${A:X}";
            Cycles += 8;
        }

        private void SRE_IND_Y() 
        {
            var address = GetIndirectIndexedAddress();
            var value = ReadByte(address);
            bool carry = IsBitSet(value, 0);
            value = (byte)(value >> 1);
            P.C = carry;
            WriteByte(address, value);
            A = (byte)(A ^ value);
            SetNZFlags(A);
            CurrentInstruction = $"SRE_IND_Y ${A:X}";
            Cycles += 8;
        }

        private void RRA_ZP() 
        {
            var address = GetZeroPageAddress();
            var value = ReadByte(address);
            var c = (byte)(IsBitSet(value, 0) ? 1 : 0);
            value = (byte)(value >> 1);
            value = (byte)(value | (P.C ? 0x80 : 0));
            P.C = ((byte)((value + A + c) / 0x100)) > 0;
            byte result = (byte)(A + value + c);
            P.V = IsOverflow(A, value, result);
            A = result;
            WriteByte(address, value);
            SetNZFlags(A);
            CurrentInstruction = $"RRA_ZP ${value:X}";
            Cycles += 5;
        }

        private void RRA_ZPX() 
        {
            var address = GetZeroPageIndexedAddress(X);
            var value = ReadByte(address);
            var c = (byte)(IsBitSet(value, 0) ? 1 : 0);
            value = (byte)(value >> 1);
            value = (byte)(value | (P.C ? 0x80 : 0));
            P.C = ((byte)((value + A + c) / 0x100)) > 0;
            byte result = (byte)(A + value + c);
            P.V = IsOverflow(A, value, result);
            A = result;
            WriteByte(address, value);
            SetNZFlags(A);
            CurrentInstruction = $"RRA_ZPX ${value:X}";
            Cycles += 6;
        }

        private void RRA_ABS() 
        {
            var address = GetAbsolutAddress();
            var value = ReadByte(address);
            var c = (byte)(IsBitSet(value, 0) ? 1 : 0);
            value = (byte)(value >> 1);
            value = (byte)(value | (P.C ? 0x80 : 0));
            P.C = ((byte)((value + A + c) / 0x100)) > 0;
            byte result = (byte)(A + value + c);
            P.V = IsOverflow(A, value, result);
            A = result;
            WriteByte(address, value);
            SetNZFlags(A);
            CurrentInstruction = $"RRA_ABS ${value:X}";
            Cycles += 6;
        }

        private void RRA_ABS_X() 
        {
            var address = GetAbsoluteIndexedAddress(X);
            var value = ReadByte(address);
            var c = (byte)(IsBitSet(value, 0) ? 1 : 0);
            value = (byte)(value >> 1);
            value = (byte)(value | (P.C ? 0x80 : 0));
            P.C = ((byte)((value + A + c) / 0x100)) > 0;
            byte result = (byte)(A + value + c);
            P.V = IsOverflow(A, value, result);
            A = result;
            WriteByte(address, value);
            SetNZFlags(A);
            CurrentInstruction = $"RRA_ABS_X ${value:X}";
            Cycles += 7;
        }

        private void RRA_ABS_Y() 
        {
            var address = GetAbsoluteIndexedAddress(Y);
            var value = ReadByte(address);
            var c = (byte)(IsBitSet(value, 0) ? 1 : 0);
            value = (byte)(value >> 1);
            value = (byte)(value | (P.C ? 0x80 : 0));
            P.C = ((byte)((value + A + c) / 0x100)) > 0;
            byte result = (byte)(A + value + c);
            P.V = IsOverflow(A, value, result);
            A = result;
            WriteByte(address, value);
            SetNZFlags(A);
            CurrentInstruction = $"RRA_ABS_Y ${value:X}";
            Cycles += 7;
        }

        private void RRA_IND_X() 
        {
            var address = GetIndexedIndirectAddress();
            var value = ReadByte(address);
            var c = (byte)(IsBitSet(value, 0) ? 1 : 0);
            value = (byte)(value >> 1);
            value = (byte)(value | (P.C ? 0x80 : 0));
            P.C = ((byte)((value + A + c) / 0x100)) > 0;
            byte result = (byte)(A + value + c);
            P.V = IsOverflow(A, value, result);
            A = result;
            WriteByte(address, value);
            SetNZFlags(A);
            CurrentInstruction = $"RRA_IND_X ${value:X}";
            Cycles += 8;
        }

        private void RRA_IND_Y() 
        {
            var address = GetIndirectIndexedAddress();
            var value = ReadByte(address);
            var c = (byte)(IsBitSet(value, 0) ? 1 : 0);
            value = (byte)(value >> 1);
            value = (byte)(value | (P.C ? 0x80 : 0));
            P.C = ((byte)((value + A + c) / 0x100)) > 0;
            byte result = (byte)(A + value + c);
            P.V = IsOverflow(A, value, result);
            A = result;
            WriteByte(address, value);
            SetNZFlags(A);
            CurrentInstruction = $"RRA_IND_Y ${value:X}";
            Cycles += 8;
        }

        private void ROL_ABS_X()
        {
            var address = GetAbsoluteIndexedAddress(X);
            var value = ReadByte(address);
            bool carry = IsBitSet(value, 7);
            value = (byte)(value << 1);
            value = (byte)(value | (P.C ? 1 : 0));
            P.C = carry;
            WriteByte(address, value);
            SetNZFlags(value);
            CurrentInstruction = $"ROL_ABS_X ${value:X}";
            Cycles += 7;
        }

        private void ROR_ACC()
        {
            var value = A;
            bool carry = IsBitSet(value, 0);
            value = (byte)(value >> 1);
            value = (byte)(value | (P.C ? 0x80 : 0));
            P.C = carry;
            A = value;
            SetNZFlags(A);
            CurrentInstruction = $"ROR_ACC ${A:X}";
            Cycles += 2;
        }

        private void ROR_ZP()
        {
            var address = GetZeroPageAddress();
            var value = ReadByte(address);
            bool carry = IsBitSet(value, 0);
            value = (byte)(value >> 1);
            value = (byte)(value | (P.C ? 0x80 : 0));
            P.C = carry;
            WriteByte(address, value);
            SetNZFlags(value);
            CurrentInstruction = $"ROR_ZP ${value:X}";
            Cycles += 5;
        }

        private void ROR_ZPX()
        {
            var address = GetZeroPageIndexedAddress(X);
            var value = ReadByte(address);
            bool carry = IsBitSet(value, 0);
            value = (byte)(value >> 1);
            value = (byte)(value | (P.C ? 0x80 : 0));
            P.C = carry;
            WriteByte(address, value);
            SetNZFlags(value);
            CurrentInstruction = $"ROR_ZPX ${value:X}";
            Cycles += 6;
        }

        private void ROR_ABS()
        {
            var address = GetAbsolutAddress();
            var value = ReadByte(address);
            bool carry = IsBitSet(value, 0);
            value = (byte)(value >> 1);
            value = (byte)(value | (P.C ? 0x80 : 0));
            P.C = carry;
            WriteByte(address, value);
            SetNZFlags(value);
            CurrentInstruction = $"ROR_ABS ${value:X}";
            Cycles += 6;
        }

        private void ROR_ABS_X()
        {
            var address = GetAbsoluteIndexedAddress(X);
            var value = ReadByte(address);
            bool carry = IsBitSet(value, 0);
            value = (byte)(value >> 1);
            value = (byte)(value | (P.C ? 0x80 : 0));
            P.C = carry;
            WriteByte(address, value);
            SetNZFlags(value);
            CurrentInstruction = $"ROR_ABS_X ${value:X}";
            Cycles += 7;
        }
        #endregion

        #region Jumps & Calls
        private void JMP_ABS()
        {
            var address = GetAbsolutAddress();
            PC = address;
            CurrentInstruction = $"JMP ${address:X}";
            Cycles += 3;
        }

        private void JMP_IND()
        {
            var ptrAdrl = ReadByte();
            var ptrAdrh = ReadByte();
            ushort ptrAdr = ptrAdrh;
            ptrAdr = (ushort)(ptrAdr << 8);
            ptrAdr += ptrAdrl;
            var addressl = ReadByte(ptrAdr);

            // 6502 bug; when fetching an indirect address for JMP, the high byte of the pointer doesn't 
            // increment if the low byte wraps around when fetching the high byte of the effective address. 
            // i.e. if ptrAdr = 0x02FF then the low byte of the effective address will be fetched from 0x0200 instead of 0x0300. 
            ptrAdrl = (byte)(ptrAdrl + 1);
            ptrAdr = ptrAdrh;
            ptrAdr = (ushort)(ptrAdr << 8);
            ptrAdr += ptrAdrl;
            var addressh = ReadByte(ptrAdr);

            ushort address = addressh;
            address = (ushort)(address << 8);
            address += addressl;

            PC = address;
            CurrentInstruction = $"JMP ${address:X}";
            Cycles += 5;
        }

        private void JSR()
        {
            var address = GetAbsolutAddress();
            var retAdr = (ushort)(PC - 1);
            var pcl = (byte)(retAdr & 0xFF);
            var pch = (byte)(retAdr >> 8);
            Push(pch);
            Push(pcl);
            PC = address;
            CurrentInstruction = $"JSR ${PC:X}";
            Cycles += 6;
        }

        private void RTS()
        {
            ReadByte();
            var pcl = Pop();
            var pch = Pop();
            ushort address = (ushort)((pcl + (pch << 8)));
            PC = address;
            PC = (ushort)(PC + 1);
            CurrentInstruction = $"RTS ${PC:X}";
            Cycles += 6;
        }
        #endregion

        #region Branches
        private void BCC()
        {
            var value = (ushort)ReadByte();
            Cycles += 2;
            if (P.C) return;
            if ((value & 0x80) > 0)
                value |= 0xff00;
            var adr = (ushort)(PC + value);

            if ((adr & 0xff00) != (PC & 0xff00))
            {
                Cycles++;
            }
            PC = adr;
            Cycles++;
            CurrentInstruction = $"BCC ${PC:X}";
        }

        private void BCS()
        {
            Cycles += 2;
            var value = (ushort)ReadByte();
            if (!P.C) return;
            if ((value & 0x80) > 0)
                value |= 0xff00;
            var adr = (ushort)(PC + value);

            if ((adr & 0xff00) != (PC & 0xff00))
            {
                Cycles++;
            }
            PC = adr;
            Cycles++;
            CurrentInstruction = $"BCS ${PC:X}";
        }

        private void BEQ()
        {
            Cycles += 2;
            var value = (ushort)ReadByte();
            if (!P.Z) return;
            if ((value & 0x80) > 0)
                value |= 0xff00;
            var adr = (ushort)(PC + value);

            if ((adr & 0xff00) != (PC & 0xff00))
            {
                Cycles++;
            }
            PC = adr;
            Cycles++;
            CurrentInstruction = $"BEQ ${PC:X}";
        }

        private void BMI()
        {
            Cycles += 2;
            var value = (ushort)ReadByte();
            if (!P.N) return;
            if ((value & 0x80) > 0)
                value |= 0xff00;
            var adr = (ushort)(PC + value);

            if ((adr & 0xff00) != (PC & 0xff00))
            {
                Cycles++;
            }
            PC = adr;
            Cycles++;
            CurrentInstruction = $"BMI ${PC:X}";
        }

        private void BNE()
        {
            Cycles += 2;
            var value = (ushort)ReadByte();
            if (P.Z) return;

            if ((value & 0x80) > 0)
                value |= 0xff00;
            var adr = (ushort)(PC + value);

            if ((adr & 0xff00) != (PC & 0xff00))
            {
                Cycles++;
            }
            PC = adr;
            Cycles++;
            CurrentInstruction = $"BNE ${PC:X}";
        }

        private void BPL()
        {
            Cycles += 2;
            var value = (ushort)ReadByte();
            if (P.N) return;
            if ((value & 0x80) > 0)
                value |= 0xff00;
            var adr = (ushort)(PC + value);

            if ((adr & 0xff00) != (PC & 0xff00))
            {
                Cycles++;
            }
            PC = adr;
            Cycles++;
            CurrentInstruction = $"BPL ${PC:X}";
        }

        private void BVC()
        {
            Cycles += 2;
            var value = (ushort)ReadByte();
            if (P.V) return;
            if ((value & 0x80) > 0)
                value |= 0xff00;
            var adr = (ushort)(PC + value);

            if ((adr & 0xff00) != (PC & 0xff00))
            {
                Cycles++;
            }
            PC = adr;
            Cycles++;
            CurrentInstruction = $"BVC ${PC:X}";
        }

        private void BVS()
        {
            Cycles += 2;
            var value = (ushort)ReadByte();
            if (!P.V) return;
            if ((value & 0x80) > 0)
                value |= 0xff00;
            var adr = (ushort)(PC + value);

            if ((adr & 0xff00) != (PC & 0xff00))
            {
                Cycles++;
            }
            PC = adr;
            Cycles++;
            CurrentInstruction = $"BVC ${PC:X}";
        }
        #endregion

        #region Status Flag Changes
        private void CLC()
        {
            P.C = false;
            CurrentInstruction = "CLC";
            Cycles += 2;
        }

        private void CLD()
        {
            P.D = false;
            CurrentInstruction = "CLD";
            Cycles += 2;
        }

        private void CLI()
        {
            P.I = false;
            CurrentInstruction = "CLI";
            Cycles += 2;
        }

        private void CLV()
        {
            P.V = false;
            CurrentInstruction = "CLV";
            Cycles += 2;
        }

        private void SEC()
        {
            P.C = true;
            CurrentInstruction = "SEC";
            Cycles += 2;
        }

        private void SED()
        {
            P.D = true;
            CurrentInstruction = "SED";
            Cycles += 2;
        }

        private void SEI()
        {
            P.I = true;
            CurrentInstruction = "SEI";
            Cycles += 2;
        }
        #endregion

        #region System Functions
        private void BRK()
        {
            var pcl = (byte)PC;
            var pch = (byte)(PC >> 8);
            var status = P.Get();
            status = (byte)(status | 0b10000);
            Push(pch);
            Push(pcl);
            Push(status);
            PC = ReadWord(InterruptVector);
            P.I = true;
            //P.B = true;
            CurrentInstruction = $"BRK ${PC:X}";
            Cycles += 7;
        }

        private void NOP()
        {
            CurrentInstruction = "NOP";
            Cycles += 2;
        }

        private void RTI()
        {
            var status = Pop();
            P.N = IsBitSet(status, 7);
            P.V = IsBitSet(status, 6);
            //P.U = IsBitSet(status, 5);
            //P.B = false;
            P.D = IsBitSet(status, 3);
            P.I = IsBitSet(status, 2);
            P.Z = IsBitSet(status, 1);
            P.C = IsBitSet(status, 0);
            var pcl = Pop();
            var pch = Pop();
            PC = (ushort)(pch << 8);
            PC += pcl;
            CurrentInstruction = $"RTI ${PC:X}";
            Cycles += 6;
        }
        #endregion

        private void InvalidOperationException()
        {
            throw new InvalidOperationException();
        }

        private void InvalidOperationZeroBytesSixCycles()
        {
            CurrentInstruction = "*NOP";
            Cycles += 6;
        }

        private void InvalidOperationOneByteSixCycles()
        {
            ReadByte();
            CurrentInstruction = "*NOP";
            Cycles += 6;
        }

        private void InvalidOperationOneByteNineCycles()
        {
            ReadByte();
            CurrentInstruction = "*NOP";
            Cycles += 9;
        }

        private void InvalidOperationOneByteTwelveCycles()
        {
            ReadByte();
            CurrentInstruction = "*NOP";
            Cycles += 12;
        }

        private void InvalidOperationTwoBytesTwelveCycles()
        {
            ReadByte();
            ReadByte();
            CurrentInstruction = "*NOP";
            Cycles += 12;
        }

        private void InvalidOperationTwoBytesFifteenCycles()
        {
            ReadByte();
            ReadByte();
            CurrentInstruction = "*NOP";
            Cycles += 15;
        }

        #region Addressing modes
        private ushort GetAbsolutAddress()
        {
            var address = ReadWord();
            return address;
        }

        // Absolute indexed addressing
        private ushort GetAbsoluteIndexedAddress(byte register)
        {
            /*     Read instructions (LDA, LDX, LDY, EOR, AND, ORA, ADC, SBC, CMP, BIT,
                                    LAX, LAE, SHS, NOP)

                    #   address  R/W description
                   --- --------- --- ------------------------------------------
                    1     PC      R  fetch opcode, increment PC
                    2     PC      R  fetch low byte of address, increment PC
                    3     PC      R  fetch high byte of address,
                                     add index register to low address byte,
                                     increment PC
                    4  address+I* R  read from effective address,
                                     fix the high byte of effective address
                    5+ address+I  R  re-read from effective address

                   Notes: I denotes either index register (X or Y).

                          * The high byte of the effective address may be invalid
                            at this time, i.e. it may be smaller by $100.

                          + This cycle will be executed only if the effective address
                            was invalid during cycle #4, i.e. page boundary was crossed.
            */
            var zpLow = ReadByte();
            var zpHigh = ReadByte();
            var low = (byte)(zpLow + register % 0x100);
            var carry = (byte)((zpLow + register) / 0x100);
            Cycles += carry;
            byte high = (byte)(zpHigh + carry);
            ushort effectiveAdr = high;
            effectiveAdr = (ushort)(effectiveAdr << 8);
            effectiveAdr += low;

            return effectiveAdr;
        }

        private ushort GetZeroPageAddress()
        {
            var address = ReadByte();
            return address;
        }

        // Zero page indexed addressing
        private ushort GetZeroPageIndexedAddress(byte register)
        {
            /*  Zero page indexed addressing

                 Read instructions (LDA, LDX, LDY, EOR, AND, ORA, ADC, SBC, CMP, BIT,
                                    LAX, NOP)

                    #   address  R/W description
                   --- --------- --- ------------------------------------------
                    1     PC      R  fetch opcode, increment PC
                    2     PC      R  fetch address, increment PC
                    3   address   R  read from address, add index register to it
                    4  address+I* R  read from effective address

                   Notes: I denotes either index register (X or Y).

                          * The high byte of the effective address is always zero,
                            i.e. page boundary crossings are not handled.
            */
            /*
            The address to be accessed by an instruction using indexed zero page addressing is calculated by
            taking the 8 bit zero page address from the instruction and adding the current value of the X register 
            to it. For example if the X register contains $0F and the instruction LDA $80,X is executed then 
            the accumulator will be loaded from $008F (e.g. $80 + $0F => $8F).

            NB:
            The address calculation wraps around if the sum of the base address and the register exceed $FF.
            If we repeat the last example but with $FF in the X register then the accumulator will be loaded 
            from $007F (e.g. $80 + $FF => $7F) and not $017F.             */


            var zp = ReadByte();
            var adr = (byte)((zp + register) % 0x100);
            return adr;
        }

        /// <summary>
        /// (Indirect,X)
        /// </summary>
        /// <param name="memory"></param>
        /// <param name="cycles"></param>
        /// <returns></returns>
        private ushort GetIndexedIndirectAddress()
        {
            /*
                    Read instructions (LDA, ORA, EOR, AND, ADC, CMP, SBC, LAX) 
                    #    address   R/W description
                   --- ----------- --- ------------------------------------------
                    1      PC       R  fetch opcode, increment PC
                    2      PC       R  fetch pointer address, increment PC
                    3    pointer    R  read from the address, add X to it
                    4   pointer+X   R  fetch effective address low
                    5  pointer+X+1  R  fetch effective address high
                    6    address    R  read from effective address

                   Note: The effective address is always fetched from zero page,
                         i.e. the zero page boundary crossing is not handled. 
             */
            var zpAdr = ReadByte();
            var l = ReadByte((byte)(zpAdr + X));
            var h = ReadByte((byte)(zpAdr + X + 1));
            ushort ret = h;
            ret = (ushort)(ret << 8);
            ret = (ushort)(ret + l);
            return ret;
            //return ReadWord((byte)(zpAdr + X));
        }

        /// <summary>
        /// (Indirect),Y
        /// </summary>
        /// <param name="memory"></param>
        /// <param name="cycles"></param>
        /// <param name="forceExtraCycle"></param>
        /// <returns></returns>
        private ushort GetIndirectIndexedAddress(bool forceExtraCycle = false)
        {
            /*
                  Read instructions (LDA, EOR, AND, ORA, ADC, SBC, CMP)

                    #    address   R/W description
                   --- ----------- --- ------------------------------------------
                    1      PC       R  fetch opcode, increment PC
                    2      PC       R  fetch pointer address, increment PC
                    3    pointer    R  fetch effective address low
                    4   pointer+1   R  fetch effective address high,
                                       add Y to low byte of effective address
                    5   address+Y*  R  read from effective address,
                                       fix high byte of effective address
                    6+  address+Y   R  read from effective address

                   Notes: The effective address is always fetched from zero page,
                          i.e. the zero page boundary crossing is not handled.

                          * The high byte of the effective address may be invalid
                            at this time, i.e. it may be smaller by $100.

                          + This cycle will be executed only if the effective address
                            was invalid during cycle #5, i.e. page boundary was crossed
             */
            var zpAdr = ReadByte();
            var zpVal = ReadByte(zpAdr);
            byte low = (byte)(zpVal + Y % 0x100);
            var carry = (byte)((zpVal + Y) / 0x100);
            Cycles += carry;
            if (carry == 0 && forceExtraCycle) Cycles++;
            var high = ReadByte((byte)(zpAdr + 1));
            high += carry;
            ushort targetAdr = high;
            targetAdr = (ushort)(targetAdr << 8);
            targetAdr += low;
            return targetAdr;
        }
        #endregion

        bool IsOverflow(byte value1, byte value2, byte result)
        {
            return ((byte)((result ^ value1) & (result ^ value2) & 0x80)) > 0;
        }

        bool IsBitSet(byte b, int pos)
        {
            return (b & (1 << pos)) != 0;
        }

        private void SetNZFlags(byte register)
        {
            P.Z = register == 0;
            P.N = IsBitSet(register, 7);
        }

        #region Internal Stack operations

        private void Push(byte value)
        {
            ushort address = (ushort)(0x100 + SP);
            WriteByte(address, value);
            DecSP();
        }

        private byte Pop()
        {
            IncSP();
            ushort address = (ushort)(0x100 + SP);
            return ReadByte(address);
        }

        private void IncSP()
        {
            SP = (byte)((SP + 1) % 0x100);
        }

        private void DecSP()
        {
            SP = (byte)((SP - 1) % 0x100);
        }
        #endregion
    }
    public struct ProcessStatusRegister
    {
        /// <summary>
        /// Processor Status
        /// |7|6|5|4|3|2|1|0|
        /// |N|V| |B|D|I|Z|C|
        /// 
        /// 0 = Carry Flag, 0 = False, 1 = true
        /// 1 = Zero Flag, 0 = Result not zero, 1 = Result zero
        /// 2 = IRQ Disable Flag, 0 = Enable, 1 = Disable
        /// 3 = Decimal Mode Flag, 0 = False, 1 = true
        /// 4 = Break Command Flag, 0 = No break, 1 = break
        /// 5 = Unused
        /// 6 = Overflow flag, 0 = false, 1 = true
        /// 7 = Negative flag, 0 = false, 1 = true
        /// </summary>

        public bool C { get; set; }
        public bool Z { get; set; }
        public bool I { get; set; }
        public bool D { get; set; }
        public bool B { get; set; }
        public bool U { get; set; } // unused
        public bool V { get; set; }
        public bool N { get; set; }
        public void Reset()
        {
            C = false;
            Z = false;
            I = true;
            D = false;
            B = false;
            U = true;
            V = false;
            N = false;
        }

        public void Print()
        {
            Console.WriteLine(ToString());
        }

        public void Set(byte s)
        {
            C = IsBitSet(s, 0);
            Z = IsBitSet(s, 1);
            I = IsBitSet(s, 2);
            D = IsBitSet(s, 3);
            B = IsBitSet(s, 4);
            U = IsBitSet(s, 5);
            V = IsBitSet(s, 6);
            N = IsBitSet(s, 7);
        }

        public byte Get()
        {
            byte b = 0;
            b = (byte)(N ? 1 : 0);
            b = (byte)(b << 1);
            b += (byte)(V ? 1 : 0);
            b = (byte)(b << 1);
            b += (byte)(U ? 1 : 0);
            b = (byte)(b << 1);
            b += (byte)(B ? 1 : 0);
            b = (byte)(b << 1);
            b += (byte)(D ? 1 : 0);
            b = (byte)(b << 1);
            b += (byte)(I ? 1 : 0);
            b = (byte)(b << 1);
            b += (byte)(Z ? 1 : 0);
            b = (byte)(b << 1);
            b += (byte)(C ? 1 : 0);
            return b;
        }
        public override string ToString()
        {
            return $"C:{Convert.ToUInt16(C)}\tZ:{Convert.ToUInt16(Z)}\tI:{Convert.ToUInt16(I)}\tD:{Convert.ToUInt16(D)}\tB:{Convert.ToUInt16(B)}\tV:{Convert.ToUInt16(V)}\tN:{Convert.ToUInt16(N)}";
        }

        private bool IsBitSet(byte b, int pos)
        {
            return (b & (1 << pos)) != 0;
        }
    }

    public enum Opcode
    {
        LDA_IM = 0xA9,
        LDA_ZP = 0xA5,
        LDA_ZPX = 0xB5,
        LDA_ABS = 0xAD,
        LDA_ABS_X = 0xBD,
        LDA_ABS_Y = 0xB9,
        LDA_IND_X = 0xA1,
        LDA_IND_Y = 0xB1,
        LDX_IM = 0xA2,
        LDX_ZP = 0xA6,
        LDX_ZPY = 0xB6,
        LDX_ABS = 0xAE,
        LDX_ABS_Y = 0xBE,
        LDY_IM = 0xA0,
        LDY_ZP = 0xA4,
        LDY_ZPX = 0xB4,
        LDY_ABS = 0xAC,
        LDY_ABS_X = 0xBC,
        STA_ZP = 0x85,
        STA_ZPX = 0x95,
        STA_ABS = 0x8D,
        STA_ABS_X = 0x9D,
        STA_ABS_Y = 0x99,
        STA_IND_X = 0x81,
        STA_IND_Y = 0x91,
        STX_ZP = 0x86,
        STX_ZPY = 0x96,
        STX_ABS = 0x8E,
        STY_ZP = 0x84,
        STY_ZPX = 0x94,
        STY_ABS = 0x8C,
        TAX = 0xAA,
        TAY = 0xA8,
        TXA = 0x8A,
        TYA = 0x98,
        TSX = 0xBA,
        TXS = 0x9A,
        PHA = 0x48,
        PHP = 0x08,
        PLA = 0x68,
        PLP = 0x28,
        AND_IM = 0x29,
        AND_ZP = 0x25,
        AND_ZPX = 0x35,
        AND_ABS = 0x2D,
        AND_ABS_X = 0x3D,
        AND_ABS_Y = 0x39,
        AND_IND_X = 0x21,
        AND_IND_Y = 0x31,
        EOR_IM = 0x49,
        EOR_ZP = 0x45,
        EOR_ZPX = 0x55,
        EOR_ABS = 0x4D,
        EOR_ABS_X = 0x5D,
        EOR_ABS_Y = 0x59,
        EOR_IND_X = 0x41,
        EOR_IND_Y = 0x51,
        ORA_IM = 0x09,
        ORA_ZP = 0x05,
        ORA_ZPX = 0x15,
        ORA_ABS = 0x0D,
        ORA_ABS_X = 0x1D,
        ORA_ABS_Y = 0x19,
        ORA_IND_X = 0x01,
        ORA_IND_Y = 0x11,
        BIT_ZP = 0x24,
        BIT_ABS = 0x2C,
        ADC_IM = 0x69,
        ADC_ZP = 0x65,
        ADC_ZPX = 0x75,
        ADC_ABS = 0x6D,
        ADC_ABS_X = 0x7D,
        ADC_ABS_Y = 0x79,
        ADC_IND_X = 0x61,
        ADC_IND_Y = 0x71,
        SBC_IM = 0xE9,
        SBC_IM_DUPLICATE = 0xEB,
        SBC_ZP = 0xE5,
        SBC_ZPX = 0xF5,
        SBC_ABS = 0xED,
        SBC_ABS_X = 0xFD,
        SBC_ABS_Y = 0xF9,
        SBC_IND_X = 0xE1,
        SBC_IND_Y = 0xF1,
        CMP_IM = 0xC9,
        CMP_ZP = 0xC5,
        CMP_ZPX = 0xD5,
        CMP_ABS = 0xCD,
        CMP_ABS_X = 0xDD,
        CMP_ABS_Y = 0xD9,
        CMP_IND_X = 0xC1,
        CMP_IND_Y = 0xD1,
        CPX_IM = 0xE0,
        CPX_ZP = 0xE4,
        CPX_ABS = 0xEC,
        CPY_IM = 0xC0,
        CPY_ZP = 0xC4,
        CPY_ABS = 0xCC,
        INC_ZP = 0xE6,
        INC_ZPX = 0xF6,
        INC_ABS = 0xEE,
        INC_ABS_X = 0xFE,
        INX = 0xE8,
        INY = 0xC8,
        DEC_ZP = 0xC6,
        DEC_ZPX = 0xD6,
        DEC_ABS = 0xCE,
        DEC_ABS_X = 0xDE,
        DEX = 0xCA,
        DEY = 0x88,
        ASL_ACC = 0x0A,
        ASL_ZP = 0x06,
        ASL_ZPX = 0x16,
        ASL_ABS = 0x0E,
        ASL_ABS_X = 0x1E,
        LSR_ACC = 0x4A,
        LSR_ZP = 0x46,
        LSR_ZPX = 0x56,
        LSR_ABS = 0x4E,
        LSR_ABS_X = 0x5E,
        ROL_ACC = 0x2A,
        ROL_ZP = 0x26,
        ROL_ZPX = 0x36,
        ROL_ABS = 0x2E,
        ROL_ABS_X = 0x3E,
        ROR_ACC = 0x6A,
        ROR_ZP = 0x66,
        ROR_ZPX = 0x76,
        ROR_ABS = 0x6E,
        ROR_ABS_X = 0x7E,
        JMP_ABS = 0x4C,
        JMP_IND = 0x6C,
        JSR = 0x20,
        RTS = 0x60,
        BCC = 0x90,
        BCS = 0xB0,
        BEQ = 0xF0,
        BMI = 0x30,
        BNE = 0xD0,
        BPL = 0x10,
        BVC = 0x50,
        BVS = 0x70,
        CLC = 0x18,
        CLD = 0xD8,
        CLI = 0x58,
        CLV = 0xB8,
        SEC = 0x38,
        SED = 0xF8,
        SEI = 0x78,
        BRK = 0x0,
        NOP = 0xEA,
        RTI = 0x40,
        LAX_IND_X = 0xA3,
        LAX_IND_Y = 0xB3,
        LAX_ZP = 0xA7,
        LAX_ABS = 0xAF,
        LAX_ABS_Y = 0xBF,
        LAX_ZPY = 0xB7,
        SAX_IND_X = 0x83,
        SAX_IM = 0x87,
        SAX_ZPY = 0x97,
        SAX_ABS = 0x8F,
        DCP_IND_X = 0xC3,
        DCP_ZP = 0xC7,
        DCP_ZP_X = 0xD7,
        DCP_ABS = 0xCF,
        DCP_ABS_Y = 0xDB,
        DCP_ABS_X = 0xDF,
        DCP_IND_Y = 0xD3,
        ISB_ZP = 0xE7,
        ISB_ZPX = 0xF7,
        ISB_ABS = 0xEF,
        ISB_ABS_X = 0xFF,
        ISB_ABS_Y = 0xFB,
        ISB_IND_X = 0xE3,
        ISB_IND_Y = 0xF3,
        SLO_ZP = 0x07,
        SLO_ZPX = 0x17,
        SLO_ABS = 0x0F,
        SLO_ABS_X = 0x1F,
        SLO_ABS_Y = 0x1B,
        SLO_IND_X = 0x03,
        SLO_IND_Y = 0x13,
        RLA_ZP = 0x27,
        RLA_ZPX = 0x37,
        RLA_ABS = 0x2F,
        RLA_ABS_X = 0x3F,
        RLA_ABS_Y = 0x3B,
        RLA_IND_X = 0x23,
        RLA_IND_Y = 0x33,
        SRE_ZP = 0x47,
        SRE_ZPX = 0x57,
        SRE_ABS = 0x4F,
        SRE_ABS_X = 0x5F,
        SRE_ABS_Y = 0x5B,
        SRE_IND_X = 0x43,
        SRE_IND_Y = 0x53,
        RRA_ZP = 0x67,
        RRA_ZPX = 0x77,
        RRA_ABS = 0x6F,
        RRA_ABS_X = 0x7F,
        RRA_ABS_Y = 0x7B,
        RRA_IND_X = 0x63,
        RRA_IND_Y = 0x73,
    }
}