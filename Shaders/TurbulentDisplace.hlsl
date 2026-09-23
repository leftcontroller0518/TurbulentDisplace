// TurbulentDisplace.hlsl
// -----------------------------------------------------------------------------
// タービュレントディスプレイス
// Improved Perlin Noise + fBM で生成した変位ベクトルで入力画像のUVをずらす。
//
// 座標系:
//   uv0.xy = 入力テクスチャのUV (0..1)
//   uv0.zw = 入力テクスチャの 1/解像度 (1px あたりのUV)
// 変位量はピクセル単位で受け取り、uv0.zw を掛けてUVへ変換する。
// -----------------------------------------------------------------------------

#define D2D_INPUT_COUNT 1
#define D2D_REQUIRES_SCENE_POSITION
#define D2D_ENTRY main
#include "d2d1effecthelpers.hlsli"

cbuffer Constants : register(b0)
{
    float Amount           : packoffset(c0.x); // 変位量 (px)
    float Size             : packoffset(c0.y); // ノイズの大きさ (px)
    float Complexity       : packoffset(c0.z); // fBM のオクターブ数
    float Evolution        : packoffset(c0.w); // ノイズ空間の移動量
    float RandomSeed       : packoffset(c1.x); // ノイズパターンの切替
    float TimeSeconds      : packoffset(c1.y); // 自動展開用の時間
    float OffsetX          : packoffset(c1.z); // ノイズ空間の横オフセット
    float OffsetY          : packoffset(c1.w); // ノイズ空間の縦オフセット
    float DisplacementMode : packoffset(c2.x); // 0:両方向 1:水平 2:垂直 3:放射
    float Pinning          : packoffset(c2.y); // 画像端の固定 (0..1)
    float EdgeWidth        : packoffset(c2.z); // 固定する端の幅 (UV)
    float Reserved         : packoffset(c2.w); // 16byte境界合わせ
};

// ハッシュ関数 (シードをノイズ空間のオフセットとして混ぜる)
float Hash21(float2 p)
{
    p += RandomSeed * float2(17.31, 43.17);
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

// Ken Perlin の Improved Noise で使われるフェード関数
float Fade(float t)
{
    return t * t * t * (t * (t * 6.0 - 15.0) + 10.0);
}

// 格子点に割り当てる勾配ベクトル
float2 GradientAt(float2 cell)
{
    float angle = Hash21(cell) * 6.28318530718;
    return float2(cos(angle), sin(angle));
}

float GradientDot(float2 cell, float2 sampleP)
{
    float2 g = GradientAt(cell);
    float2 rel = sampleP - cell;
    return dot(g, rel);
}

// 2D Perlin Noise (-1..1 付近)
float PerlinNoise2D(float2 p)
{
    float2 cell = floor(p);
    float2 local = frac(p);
    float2 f = float2(Fade(local.x), Fade(local.y));

    float n00 = GradientDot(cell + float2(0.0, 0.0), p);
    float n10 = GradientDot(cell + float2(1.0, 0.0), p);
    float n01 = GradientDot(cell + float2(0.0, 1.0), p);
    float n11 = GradientDot(cell + float2(1.0, 1.0), p);

    float nx0 = lerp(n00, n10, f.x);
    float nx1 = lerp(n01, n11, f.x);
    return lerp(nx0, nx1, f.y);
}

// 0..1 へ正規化
float PerlinNoise01(float2 p)
{
    return PerlinNoise2D(p) * 0.5 + 0.5;
}

// Fractal Brownian Motion
float FractalNoise(float2 p, int octaves)
{
    float value = 0.0;
    float amplitude = 0.5;
    float frequency = 1.0;
    float totalAmplitude = 0.0;

    [loop]
    for (int i = 0; i < 10; i++)
    {
        if (i >= octaves)
            break;

        value += PerlinNoise01(p * frequency) * amplitude;
        totalAmplitude += amplitude;
        frequency *= 2.0;
        amplitude *= 0.5;
    }

    return value / max(totalAmplitude, 1e-5);
}

// 画像端を固定するための減衰係数
float GetPinningFactor(float2 uv)
{
    if (Pinning <= 0.0)
        return 1.0;

    float width = max(EdgeWidth, 0.0001);
    float left = smoothstep(0.0, width, uv.x);
    float right = smoothstep(0.0, width, 1.0 - uv.x);
    float top = smoothstep(0.0, width, uv.y);
    float bottom = smoothstep(0.0, width, 1.0 - uv.y);
    float edgeFactor = left * right * top * bottom;
    return lerp(1.0, edgeFactor, saturate(Pinning));
}

D2D_PS_ENTRY(main)
{
    float2 uv = D2DGetInputCoordinate(0);
    float4 source = D2DGetInput(0);

    if (Amount <= 0.0)
        return source;

    float safeSize = max(Size, 1.0);

    // Direct2D may render an effect in multiple input tiles. uv0 is relative
    // to the current tile, so derive the image-local position from the stable
    // scene position to prevent the noise phase from resetting at tile edges.
    // ノイズ空間の座標 (px)。展開・オフセットはノイズ空間を平行移動させる。
    // SCENE_POSITION は入力タイルごとにリセットされないため、ここで
    // uv を使うとタイル境界でノイズパターンが再開する問題を避けられる。
    float2 noisePosition = D2DGetScenePosition().xy / safeSize;
    noisePosition += float2(OffsetX, OffsetY);
    noisePosition += float2(Evolution * 0.13, Evolution * 0.071);
    noisePosition += TimeSeconds * float2(0.13, 0.071);

    int octaves = (int)clamp(Complexity, 1.0, 10.0);

    // X方向とY方向で別のオフセットを与え、変位方向の偏りを避ける
    float nx = FractalNoise(noisePosition + float2(13.1, 7.2), octaves);
    float ny = FractalNoise(noisePosition + float2(91.4, 42.8), octaves);
    float2 displacement = float2(nx * 2.0 - 1.0, ny * 2.0 - 1.0);

    int mode = (int)DisplacementMode;
    if (mode == 1)
    {
        displacement.y = 0.0;
    }
    else if (mode == 2)
    {
        displacement.x = 0.0;
    }
    else if (mode == 3)
    {
        float2 radialDirection = uv - float2(0.5, 0.5);
        float radialLen = length(radialDirection);
        radialDirection = radialLen > 1e-5 ? radialDirection / radialLen : float2(0.0, 0.0);

        float radialValue = FractalNoise(noisePosition, octaves);
        displacement = radialDirection * (radialValue * 2.0 - 1.0);
    }

    displacement *= GetPinningFactor(uv);

    // Sample in scene-pixel space so Direct2D can resolve the correct source
    // tile. Sampling Texture2D with tile-local UVs causes split output.
    return D2DSampleInputAtOffset(0, displacement * Amount);
}
