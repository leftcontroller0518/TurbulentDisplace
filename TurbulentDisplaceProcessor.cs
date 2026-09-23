using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace TurbulentDisplace
{
    /// <summary>
    /// タービュレントディスプレイスのフレーム処理プロセッサ。
    /// 入力画像のUVをノイズでずらすだけなので、出力矩形は入力矩形と同じ。
    /// </summary>
    internal class TurbulentDisplaceProcessor : IVideoEffectProcessor
    {
        readonly TurbulentDisplaceEffect item;
        readonly TurbulentDisplaceCustomEffect effect;
        ID2D1Image? input;

        public TurbulentDisplaceProcessor(IGraphicsDevicesAndContext devices, TurbulentDisplaceEffect item)
        {
            this.item = item;
            effect = new TurbulentDisplaceCustomEffect(devices);
        }

        public ID2D1Image Output => effect.Output;

        public void SetInput(ID2D1Image? input)
        {
            this.input = input;
            effect.SetInput(0, input, true);
        }

        public void ClearInput()
        {
            input = null;
            effect.SetInput(0, null, true);
        }

        public DrawDescription Update(EffectDescription effectDescription)
        {
            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = Math.Max(effectDescription.FPS, 1);

            effect.Amount = Sanitize(item.Amount.GetValue(frame, length, fps), 0f, TurbulentDisplaceCustomEffect.MaxAmount);
            effect.Size = Sanitize(item.Size.GetValue(frame, length, fps), 1f, TurbulentDisplaceCustomEffect.MaxSize);
            effect.Complexity = Sanitize(item.Complexity.GetValue(frame, length, fps), 1f, 10f);
            effect.Evolution = Sanitize(item.Evolution.GetValue(frame, length, fps), -100000f, 100000f);
            effect.OffsetX = Sanitize(item.OffsetX.GetValue(frame, length, fps), -100000f, 100000f);
            effect.OffsetY = Sanitize(item.OffsetY.GetValue(frame, length, fps), -100000f, 100000f);
            effect.RandomSeed = Sanitize(item.RandomSeed.GetValue(frame, length, fps), 0f, 100000f);
            effect.TimeSeconds = frame / (float)fps;
            effect.DisplacementMode = (float)(int)item.DisplacementMode;
            effect.Pinning = Sanitize(item.Pinning.GetValue(frame, length, fps), 0f, 1f);
            effect.EdgeWidth = Sanitize(item.EdgeWidth.GetValue(frame, length, fps), 0.0001f, 0.5f);

            return effectDescription.DrawDescription;
        }

        static float Sanitize(double value, float minimum, float maximum)
        {
            if (!double.IsFinite(value))
                return minimum;
            return (float)Math.Clamp(value, minimum, maximum);
        }

        public void Dispose()
        {
            effect.Dispose();
        }
    }

    /// <summary>
    /// Direct2D カスタムピクセルシェーダーエフェクトのラッパー。
    /// 画素をずらすだけで矩形は変えないため、入力と出力の矩形を一致させる。
    /// </summary>
    internal sealed class TurbulentDisplaceCustomEffect(IGraphicsDevicesAndContext devices) : D2D1CustomShaderEffectBase(Create<EffectImpl>(devices))
    {
        public const float MaxAmount = 4096f;
        public const float MaxSize = 4096f;

        public float Amount { get => GetFloatValue((int)EffectImpl.Properties.Amount); set => SetValue((int)EffectImpl.Properties.Amount, value); }
        public float Size { get => GetFloatValue((int)EffectImpl.Properties.Size); set => SetValue((int)EffectImpl.Properties.Size, value); }
        public float Complexity { get => GetFloatValue((int)EffectImpl.Properties.Complexity); set => SetValue((int)EffectImpl.Properties.Complexity, value); }
        public float Evolution { get => GetFloatValue((int)EffectImpl.Properties.Evolution); set => SetValue((int)EffectImpl.Properties.Evolution, value); }
        public float RandomSeed { get => GetFloatValue((int)EffectImpl.Properties.RandomSeed); set => SetValue((int)EffectImpl.Properties.RandomSeed, value); }
        public float TimeSeconds { get => GetFloatValue((int)EffectImpl.Properties.TimeSeconds); set => SetValue((int)EffectImpl.Properties.TimeSeconds, value); }
        public float OffsetX { get => GetFloatValue((int)EffectImpl.Properties.OffsetX); set => SetValue((int)EffectImpl.Properties.OffsetX, value); }
        public float OffsetY { get => GetFloatValue((int)EffectImpl.Properties.OffsetY); set => SetValue((int)EffectImpl.Properties.OffsetY, value); }
        public float DisplacementMode { get => GetFloatValue((int)EffectImpl.Properties.DisplacementMode); set => SetValue((int)EffectImpl.Properties.DisplacementMode, value); }
        public float Pinning { get => GetFloatValue((int)EffectImpl.Properties.Pinning); set => SetValue((int)EffectImpl.Properties.Pinning, value); }
        public float EdgeWidth { get => GetFloatValue((int)EffectImpl.Properties.EdgeWidth); set => SetValue((int)EffectImpl.Properties.EdgeWidth, value); }

        [CustomEffect(1)]
        private sealed class EffectImpl : D2D1CustomShaderEffectImplBase<EffectImpl>
        {
            // HLSL 側の cbuffer Constants (register b0) と 1:1 で対応する定数バッファ。
            // すべて float のため、並び順がそのまま packoffset(c0.x).. と一致する（12 float = 48 byte）。
            [StructLayout(LayoutKind.Sequential)]
            struct ConstantBuffer
            {
                public float Amount;
                public float Size;
                public float Complexity;
                public float Evolution;
                public float RandomSeed;
                public float TimeSeconds;
                public float OffsetX;
                public float OffsetY;
                public float DisplacementMode;
                public float Pinning;
                public float EdgeWidth;
                public float Reserved;
            }

            ConstantBuffer constants;

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Amount)]
            public float Amount
            {
                get => constants.Amount;
                set { constants.Amount = float.IsFinite(value) ? Math.Clamp(value, 0f, MaxAmount) : 0f; UpdateConstants(); }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Size)]
            public float Size
            {
                get => constants.Size;
                set { constants.Size = float.IsFinite(value) ? Math.Clamp(value, 1f, MaxSize) : 64f; UpdateConstants(); }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Complexity)]
            public float Complexity
            {
                get => constants.Complexity;
                set { constants.Complexity = float.IsFinite(value) ? Math.Clamp(value, 1f, 10f) : 4f; UpdateConstants(); }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Evolution)]
            public float Evolution
            {
                get => constants.Evolution;
                set { constants.Evolution = float.IsFinite(value) ? value : 0f; UpdateConstants(); }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.RandomSeed)]
            public float RandomSeed
            {
                get => constants.RandomSeed;
                set { constants.RandomSeed = float.IsFinite(value) ? value : 0f; UpdateConstants(); }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.TimeSeconds)]
            public float TimeSeconds
            {
                get => constants.TimeSeconds;
                set { constants.TimeSeconds = float.IsFinite(value) ? value : 0f; UpdateConstants(); }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.OffsetX)]
            public float OffsetX
            {
                get => constants.OffsetX;
                set { constants.OffsetX = float.IsFinite(value) ? value : 0f; UpdateConstants(); }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.OffsetY)]
            public float OffsetY
            {
                get => constants.OffsetY;
                set { constants.OffsetY = float.IsFinite(value) ? value : 0f; UpdateConstants(); }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.DisplacementMode)]
            public float DisplacementMode
            {
                get => constants.DisplacementMode;
                set { constants.DisplacementMode = float.IsFinite(value) ? Math.Clamp(value, 0f, 3f) : 0f; UpdateConstants(); }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.Pinning)]
            public float Pinning
            {
                get => constants.Pinning;
                set { constants.Pinning = float.IsFinite(value) ? Math.Clamp(value, 0f, 1f) : 0f; UpdateConstants(); }
            }

            [CustomEffectProperty(PropertyType.Float, (int)Properties.EdgeWidth)]
            public float EdgeWidth
            {
                get => constants.EdgeWidth;
                set { constants.EdgeWidth = float.IsFinite(value) ? Math.Clamp(value, 0.0001f, 0.5f) : 0.05f; UpdateConstants(); }
            }

            public EffectImpl() : base(ShaderResourceUri.Get("TurbulentDisplace"))
            {
                constants.Size = 64f;
                constants.Complexity = 4f;
                constants.EdgeWidth = 0.05f;
            }

            const int ConstantBufferByteSize = 48; // 12 float * 4 byte, 16 byte 境界に整列済み

            protected override void UpdateConstants()
            {
                if (drawInformation is null)
                    return;

                Span<byte> buffer = stackalloc byte[ConstantBufferByteSize];
                MemoryMarshal.Write(buffer, in constants);
                drawInformation.SetPixelShaderConstantBuffer(buffer);
            }

            public override void MapInputRectsToOutputRect(
                Vortice.RawRect[] inputRects,
                Vortice.RawRect[] inputOpaqueSubRects,
                out Vortice.RawRect outputRect,
                out Vortice.RawRect outputOpaqueSubRect)
            {
                base.MapInputRectsToOutputRect(
                    inputRects,
                    inputOpaqueSubRects,
                    out outputRect,
                    out outputOpaqueSubRect);
            }

            public override void MapOutputRectToInputRects(
                Vortice.RawRect outputRect,
                Vortice.RawRect[] inputRects)
            {
                var margin = (int)Math.Ceiling(Math.Clamp(constants.Amount, 0f, MaxAmount));
                inputRects[0] = new Vortice.RawRect(
                    outputRect.Left - margin,
                    outputRect.Top - margin,
                    outputRect.Right + margin,
                    outputRect.Bottom + margin);
            }

            public enum Properties
            {
                Amount,
                Size,
                Complexity,
                Evolution,
                RandomSeed,
                TimeSeconds,
                OffsetX,
                OffsetY,
                DisplacementMode,
                Pinning,
                EdgeWidth,
            }
        }
    }
}
