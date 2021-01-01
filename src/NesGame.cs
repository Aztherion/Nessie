using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Nessie
{
    class NesGame : Game
    {
        readonly GraphicsDeviceManager _graphics;
        private readonly Bus _nes;
        SpriteBatch _spriteBatch;
        SpriteFont _font;
        Texture2D _frameCanvas;
        Texture2D[] _patternTableCanvas;
        Texture2D[] _paletteCanvas;

        public NesGame()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            _nes = new Bus();
            _graphics.PreferredBackBufferWidth = 1024;
            _graphics.PreferredBackBufferHeight = 768;
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            _frameCanvas = new Texture2D(GraphicsDevice, 256, 240);
            _patternTableCanvas = new Texture2D[2];
            _patternTableCanvas[0] = new Texture2D(GraphicsDevice, 128, 128);
            _patternTableCanvas[1] = new Texture2D(GraphicsDevice, 128, 128);
            _paletteCanvas = new Texture2D[8];
            for (var i = 0; i < 8; i++)
            {
                _paletteCanvas[i] = new Texture2D(GraphicsDevice, 40, 10);
            }
            _font = Content.Load<SpriteFont>("Font");
            var cartridge = new Cartridge("Content/roms/smb.nes");
            _nes.InsertCartridge(cartridge);
            _nes.Reset();
        }

        long renderFrameMs = 0;
        long drawMs = 0;
        byte frameCount = 0;
        long drawCount = 0;
        Stopwatch sw = new Stopwatch();
        bool runEmulation = false;
        List<Keys> downKeys = new List<Keys>();
        double residualTime = 0;
        double previousFrameGameTime = 0;
        double fps = 0;
        Stopwatch runSw = new Stopwatch();
        long elapsedRunMs = 0;

        private void ExportNametable(int nametableId)
        {
            var nametable = _nes.Ppu.GetNameTable(nametableId);
            var filename = @"c:\tmp\nametable_" + nametableId + ".txt";
            if (File.Exists(filename)) File.Delete(filename);
            using (var fs = new FileStream(filename, FileMode.OpenOrCreate))
            {
                using (var sw = new StreamWriter(fs))
                {
                    for (var y = 0; y < 32; y++)
                    {
                        for (var x = 0; x < 32; x++)
                        {
                            var ix = y * 32 + x;
                            var s = nametable[ix].ToString("X").PadLeft(2, '0') + " ";
                            sw.Write(s);
                        }
                        sw.WriteLine();
                    }
                }
            }
        }

        protected override void Update(GameTime gameTime)
        {
            _nes.Controller[0] = 0x0;
            _nes.Controller[0] |= (byte)(Keyboard.GetState().IsKeyDown(Keys.X) ? 0x80 : 0x00);
            _nes.Controller[0] |= (byte)(Keyboard.GetState().IsKeyDown(Keys.Z) ? 0x40 : 0x00);
            _nes.Controller[0] |= (byte)(Keyboard.GetState().IsKeyDown(Keys.A) ? 0x20 : 0x00);
            _nes.Controller[0] |= (byte)(Keyboard.GetState().IsKeyDown(Keys.S) ? 0x10 : 0x00);
            _nes.Controller[0] |= (byte)(Keyboard.GetState().IsKeyDown(Keys.Up) ? 0x08 : 0x00);
            _nes.Controller[0] |= (byte)(Keyboard.GetState().IsKeyDown(Keys.Down) ? 0x04 : 0x00);
            _nes.Controller[0] |= (byte)(Keyboard.GetState().IsKeyDown(Keys.Left) ? 0x02 : 0x00);
            _nes.Controller[0] |= (byte)(Keyboard.GetState().IsKeyDown(Keys.Right) ? 0x01 : 0x00);

            if (Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            if (Keyboard.GetState().IsKeyDown(Keys.R))
            {
                _nes.Reset();
            }

            if (Keyboard.GetState().IsKeyDown(Keys.E) && !downKeys.Contains(Keys.E))
            {
                downKeys.Add(Keys.E);
                ExportNametable(0);
                ExportNametable(1);
            }
            else if (Keyboard.GetState().IsKeyUp(Keys.E) && downKeys.Contains(Keys.E))
            {
                downKeys.Remove(Keys.E);
            }
            /*
            if (Keyboard.GetState().IsKeyDown(Keys.D) && !downKeys.Contains(Keys.D))
            {
                downKeys.Add(Keys.D);
                _nes.Ppu.RequestFrameDump = true;
            } else if(Keyboard.GetState().IsKeyUp(Keys.D) && downKeys.Contains(Keys.D))
            {
                downKeys.Remove(Keys.D);
            }
            */

            if (Keyboard.GetState().IsKeyDown(Keys.Space) && !downKeys.Contains(Keys.Space))
            {
                downKeys.Add(Keys.Space);
                runEmulation = !runEmulation;
                if (runEmulation)
                {
                    runSw.Reset();
                    runSw.Start();
                    frameCount = 0;
                }
                if (!runEmulation)
                {
                    runSw.Stop();
                    elapsedRunMs = runSw.ElapsedMilliseconds;
                }
            }
            else if (Keyboard.GetState().IsKeyUp(Keys.Space) && downKeys.Contains(Keys.Space))
            {
                downKeys.Remove(Keys.Space);
            }

            if (Keyboard.GetState().IsKeyDown(Keys.P) && !downKeys.Contains(Keys.P))
            {
                downKeys.Add(Keys.P);
                _palette = (byte)((_palette + 1) % 8);
            } else if (Keyboard.GetState().IsKeyUp(Keys.P) && downKeys.Contains(Keys.P))
            {
                downKeys.Remove(Keys.P);
            }

            if (!runEmulation)
            {
                if (Keyboard.GetState().IsKeyDown(Keys.C) && !downKeys.Contains(Keys.C))
                {
                    downKeys.Add(Keys.C);
                    do
                    {
                        _nes.Clock();
                    } while (!_nes.Cpu.Complete);
                } else if (Keyboard.GetState().IsKeyUp(Keys.C) && downKeys.Contains(Keys.C))
                {
                    downKeys.Remove(Keys.C);
                }

                if (Keyboard.GetState().IsKeyDown(Keys.F) && !downKeys.Contains(Keys.F))
                {
                    downKeys.Add(Keys.F);
                    sw.Reset();
                    sw.Start();

                    do
                    {
                        _nes.Clock();
                    } while (!_nes.Cpu.Complete);
                    do
                    {
                        _nes.Clock();
                    } while (!_nes.Ppu.FrameComplete);
                    _nes.Ppu.FrameComplete = false;
                    sw.Stop();

                    renderFrameMs = sw.ElapsedMilliseconds;
                } else if (Keyboard.GetState().IsKeyUp(Keys.F) && downKeys.Contains(Keys.F))
                {
                    downKeys.Remove(Keys.F);
                }
            }
            base.Update(gameTime);
        }

        byte _palette = 0;

        protected override void Draw(GameTime gameTime)
        {
            if (runEmulation)
            {
                if (residualTime >= 0)
                {
                    residualTime -= gameTime.ElapsedGameTime.TotalMilliseconds;
                }
                else
                {
                    var now = runSw.ElapsedMilliseconds;

                    fps = 1000f / (now - previousFrameGameTime);

                    residualTime = (1000f / 60f) - gameTime.ElapsedGameTime.TotalMilliseconds;
                    sw.Reset();
                    sw.Start();

                    do
                    {
                        _nes.Clock();
                    } while (!_nes.Cpu.Complete);
                    do
                    {
                        _nes.Clock();
                    } while (!_nes.Ppu.FrameComplete);
                    _nes.Ppu.FrameComplete = false;
                    sw.Stop();

                    renderFrameMs = sw.ElapsedMilliseconds;
                    residualTime -= renderFrameMs;
                    previousFrameGameTime = now;
                    frameCount++;
                }

            }
            sw.Restart();
            sw.Start();
            GraphicsDevice.Clear(Color.CornflowerBlue);
            /*
            // HACK! only used for debugging purposes. 
            var patternTable = _nes.Ppu.GetPatternTable(1, _palette);
            var data = new UInt32[341 * 261];
            var nametable = _nes.Ppu.GetNameTable(0);
            for (var y = 0; y < 30; y++)
            {
                for (var x = 0;x < 32; x++)
                {
                    var ix = y * 32 + x;
                    var id = nametable[ix];

                    //var left = (id & 0x0F) << 3;
                    //var top = ((id >> 4) & 0x0F) << 3;

                    var left = id & 0x0F;
                    var top = (id >> 0x4) & 0x0F;

                    for(var py = 0; py < 8; py++)
                    {
                        for(var px = 0; px < 8; px++)
                        {

                            var srcIx = (((top*8) + py) * 128) + (left * 8) + px;
                            //var srcIx = (py + left * 8) + (px + top * 8);
                            var dstIx = ((y*8) + py) * 341 + (x*8) + px;
                            data[dstIx] = patternTable[srcIx];
                        }
                    }

                }
            }
            */
            _frameCanvas.SetData<UInt32>(_nes.Ppu.GetActiveFrame(), 0, 256 * 240);
            _patternTableCanvas[0].SetData<UInt32>(_nes.Ppu.GetPatternTable(0, _palette), 0, 128 * 128);
            _patternTableCanvas[1].SetData<UInt32>(_nes.Ppu.GetPatternTable(1, _palette), 0, 128 * 128);
            for (var i = 0; i < 8; i++)
            {
                _paletteCanvas[i].SetData<UInt32>(GetPaletteData((byte)i), 0, 10 * 40);
            }

            _spriteBatch.Begin();
            _spriteBatch.Draw(_frameCanvas, new Rectangle(400, 10, 256 * 2, 240 * 2), Color.White);
            _spriteBatch.Draw(_patternTableCanvas[0], new Rectangle(400, 520, 128, 128), Color.White);
            _spriteBatch.Draw(_patternTableCanvas[1], new Rectangle(535, 520, 128, 128), Color.White);

            for (var i = 0; i < 8; i++)
            {
                _spriteBatch.Draw(_paletteCanvas[i], new Rectangle(400 + (i * 45), 500, 40, 10), Color.White);
            }
            _spriteBatch.DrawString(_font, $"A: 0x{_nes.Cpu.A:X}", new Vector2(10, 10), Color.Black);
            _spriteBatch.DrawString(_font, $"X: 0x{_nes.Cpu.X:X}", new Vector2(10, 30), Color.Black);
            _spriteBatch.DrawString(_font, $"Y: 0x{_nes.Cpu.Y:X}", new Vector2(10, 50), Color.Black);
            _spriteBatch.DrawString(_font, $"PC: 0x{_nes.Cpu.PC:X}", new Vector2(10, 70), Color.Black);
            _spriteBatch.DrawString(_font, $"SP: 0x{_nes.Cpu.SP:X}", new Vector2(10, 90), Color.Black);
            _spriteBatch.DrawString(_font, $"PS: {GetStatusString()}", new Vector2(10, 110), Color.Black);
            _spriteBatch.DrawString(_font, $"INS: {_nes.Cpu.CurrentInstruction.Replace("$", "0x").Replace("_", " ")}", new Vector2(10, 130), Color.Black);
            _spriteBatch.DrawString(_font, $"Render MS: {renderFrameMs:F0}", new Vector2(10, 150), Color.Black);
            _spriteBatch.DrawString(_font, $"Draw MS: {drawMs:F0}", new Vector2(10, 170), Color.Black);
            _spriteBatch.DrawString(_font, $"Frame Count: {frameCount:F0}", new Vector2(10, 190), Color.Black);
            _spriteBatch.DrawString(_font, $"SysClk: {_nes.SystemClockCounter:F0}", new Vector2(10, 210), Color.Black);
            _spriteBatch.DrawString(_font, $"FPS: {fps:F2}", new Vector2(10, 230), Color.Black);
            _spriteBatch.DrawString(_font, $"Run: {runEmulation}", new Vector2(10, 250), Color.Black);
            _spriteBatch.DrawString(_font, $"Run MS: {elapsedRunMs:0}", new Vector2(10, 270), Color.Black);
            _spriteBatch.DrawString(_font, $"Draw count: {drawCount:0}", new Vector2(10, 290), Color.Black);

            for(var i = 0; i < 16; i++)
            {
                var label = i.ToString("X").PadLeft(2, '0');
                var spriteY = _nes.Ppu.OAM[i * 4 + 0].ToString("F0").PadLeft(2, '0');
                var spriteX = _nes.Ppu.OAM[i * 4 + 3].ToString("F0").PadLeft(2, '0');
                var spriteId = _nes.Ppu.OAM[i * 4 + 1].ToString("X").PadLeft(2, '0');
                var spriteAttrib = _nes.Ppu.OAM[i * 4 + 2].ToString("X").PadLeft(2, '0');
                _spriteBatch.DrawString(_font, $"{label} {spriteX}, {spriteY}, ID {spriteId} AT {spriteAttrib}", new Vector2(10, 310 + (i * 20)), Color.Black);
            }

            _spriteBatch.End();
            base.Draw(gameTime);
            sw.Stop();
            drawMs = sw.ElapsedMilliseconds;
            drawCount++;
        }

        private UInt32[] GetPaletteData(byte palette)
        {
            var ret = new UInt32[10 * 40];

            var pixels = _nes.Ppu.GetPalettePixels(palette);
            for(var y = 0; y < 10; y++)
            {
                for(var x = 0; x < 40; x++)
                {
                    var ix = x / 10;
                    var pixel = pixels[ix];
                    ret[y * 40 + x] = pixel.ToUInt32();
                }
            }

            return ret;
        }

        private string GetStatusString()
        {
            var n = _nes.Cpu.P.N ? "1" : "0";
            var v = _nes.Cpu.P.V ? "1" : "0";
            var b = _nes.Cpu.P.B ? "1" : "0";
            var d = _nes.Cpu.P.D ? "1" : "0";
            var i = _nes.Cpu.P.I ? "1" : "0";
            var z = _nes.Cpu.P.Z ? "1" : "0";
            var c = _nes.Cpu.P.C ? "1" : "0";
            var s = $"N:{n}  V:{v}  B:{b}  D:{d}  I:{i}  Z:{z}  C:{c}";
            return s;
        }
    }
}
