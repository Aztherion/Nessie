using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Diagnostics;

namespace Nessie
{
    class NesGame : Game
    {
        readonly GraphicsDeviceManager _graphics;
        private readonly Bus _nes;
        SpriteBatch _spriteBatch;
        SpriteFont _font;
        Texture2D _canvas;

        public NesGame()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            _nes = new Bus();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            _canvas = new Texture2D(GraphicsDevice, 341, 261);
            _font = Content.Load<SpriteFont>("Font");
            var cartridge = new Cartridge("nestest.nes");
            _nes.InsertCartridge(cartridge);
            _nes.Reset();
        }

        bool runSingleTick = true;
        bool runSingleFrame = true;
        long renderFrameMs = 0;
        long drawMs = 0;
        byte frameCount = 0;
        Stopwatch sw = new Stopwatch();
        protected override void Update(GameTime gameTime)
        {
            if (Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            if (Keyboard.GetState().IsKeyDown(Keys.C) && runSingleTick)
            {
                runSingleTick = false;
                do
                {
                    _nes.Clock();
                } while (!_nes.Cpu.Complete);
            }
            if (Keyboard.GetState().IsKeyUp(Keys.C))
            {
                runSingleTick = true;
            }

            if (Keyboard.GetState().IsKeyDown(Keys.F1) && runSingleFrame)
            { 
                sw.Reset();
                sw.Start();
                runSingleFrame = false;
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
            }

            if (Keyboard.GetState().IsKeyUp(Keys.F1))
            {
                runSingleFrame = true;
            }

            if (Keyboard.GetState().IsKeyDown(Keys.R))
            {
                _nes.Reset();
            }
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            sw.Restart();
            sw.Start();
            GraphicsDevice.Clear(Color.CornflowerBlue);
            _canvas.SetData<UInt32>(_nes.Ppu.GetActiveFrame(), 0, 341 * 261);
            _spriteBatch.Begin();

            _spriteBatch.Draw(_canvas, new Rectangle(400, 10, 341, 261), Color.White);
            //_spriteBatch.Draw(_canvas, new Rectangle(10, 10, 1, 1), Color.White);
            _spriteBatch.DrawString(_font, $"A: 0x{_nes.Cpu.A:X}", new Vector2(10, 10), Color.Black);
            _spriteBatch.DrawString(_font, $"X: 0x{_nes.Cpu.X:X}", new Vector2(10, 30), Color.Black);
            _spriteBatch.DrawString(_font, $"Y: 0x{_nes.Cpu.Y:X}", new Vector2(10, 50), Color.Black);
            _spriteBatch.DrawString(_font, $"PC: 0x{_nes.Cpu.PC:X}", new Vector2(10, 70), Color.Black);
            _spriteBatch.DrawString(_font, $"SP: 0x{_nes.Cpu.SP:X}", new Vector2(10, 90), Color.Black);
            _spriteBatch.DrawString(_font, $"PS: {GetStatusString()}", new Vector2(10, 110), Color.Black);
            _spriteBatch.DrawString(_font, $"INS: {_nes.Cpu.CurrentInstruction.Replace("$", "0x").Replace("_", " ")}", new Vector2(10, 130), Color.Black);
            _spriteBatch.DrawString(_font, $"Render MS: {renderFrameMs:F0}", new Vector2(10, 150), Color.Black);
            _spriteBatch.DrawString(_font, $"Draw MS: {drawMs:F0}", new Vector2(10, 170), Color.Black);
            _spriteBatch.DrawString(_font, $"FrameCount: {frameCount:F0}", new Vector2(10, 190), Color.Black);
            _spriteBatch.End();
            base.Draw(gameTime);
            sw.Stop();
            drawMs = sw.ElapsedMilliseconds;
            frameCount++;
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
