using Godot;
using Hollowfall.Core.Textures;

namespace Hollowfall.Game;

/// <summary>
/// The water surface material. Water wears one 16 by 16 tile of the shared block atlas, so scrolling its UVs the usual way would
/// drag the neighbouring tiles (snow, gravel) across the lake. This shader keeps the animation inside the water tile instead:
/// it turns the mesh's atlas coordinates back into a position within the tile, moves that position (a slow drift plus a
/// ripple that shifts whole pixel rows), wraps it with <c>fract</c> over the tile and only then looks the atlas up. The tile
/// wraps seamlessly by construction (<see cref="BlockTextures"/>), so the motion has no seam, and the atlas is sampled with the
/// nearest filter so no neighbour's pixels can bleed in. Built in code because the project is C# only.
/// </summary>
public static class EffectsWater
{
    private const string ShaderCode = """
        shader_type spatial;
        render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_burley, specular_schlick_ggx;

        uniform sampler2D atlas : source_color, filter_nearest, repeat_disable;
        // The part of the atlas the mesh's coordinates cover (the water tile pulled in half a pixel) and the whole tile.
        uniform vec4 inset_rect;
        uniform vec4 tile_rect;
        uniform float gain = 1.1;
        uniform float tile_pixels = 16.0;
        uniform vec2 drift = vec2(1.5, 0.9);
        uniform float ripple = 1.5;

        void fragment() {
            // Position within the tile in pixels, 0 to tile_pixels, from the mesh's atlas coordinates.
            vec2 q = (UV - inset_rect.xy) / (inset_rect.zw - inset_rect.xy) * tile_pixels;
            float t = TIME;
            // Ripple: each pixel row slides sideways by whole pixels, and each column up or down, so it stays chunky.
            q.x += floor(sin(q.y * 0.75 + t * 1.7) * ripple + 0.5);
            q.y += floor(sin(q.x * 0.75 + t * 1.3) * ripple + 0.5);
            q += drift * t;
            // Wrap inside the tile, then back into atlas space. Nothing outside the tile is ever read.
            vec2 p = fract(q / tile_pixels);
            vec4 texel = texture(atlas, tile_rect.xy + p * (tile_rect.zw - tile_rect.xy));
            ALBEDO = COLOR.rgb * texel.rgb * gain;
            ALPHA = COLOR.a;
            ROUGHNESS = 0.2;
        }
        """;

    private static ShaderMaterial? _material;

    /// <summary>The shared water material.</summary>
    public static ShaderMaterial Material => _material ??= Create();

    /// <summary>The rectangle of the whole water tile in atlas space (u0, v0, u1, v1), not pulled in at the edges.</summary>
    public static Vector4 TileRect()
    {
        var (u0, v0, u1, v1) = BlockTextures.Rect(Tile.Water);
        float hu = 0.5f / BlockTextures.Width, hv = 0.5f / BlockTextures.Height;
        return new Vector4(u0 - hu, v0 - hv, u1 + hu, v1 + hv);
    }

    private static ShaderMaterial Create()
    {
        var (u0, v0, u1, v1) = BlockTextures.Rect(Tile.Water);
        var m = new ShaderMaterial { Shader = new Shader { Code = ShaderCode } };
        m.SetShaderParameter("atlas", ChunkMeshBuilder.Atlas);
        m.SetShaderParameter("inset_rect", new Vector4(u0, v0, u1, v1));
        m.SetShaderParameter("tile_rect", TileRect());
        m.SetShaderParameter("gain", BlockTextures.Gain);
        m.SetShaderParameter("tile_pixels", (float)BlockTextures.TileSize);
        return m;
    }
}
