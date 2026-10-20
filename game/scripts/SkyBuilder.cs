using Godot;
using Hollowfall.Core.Textures;

namespace Hollowfall.Game;

/// <summary>
/// The surface sky, built in code: a sky shader (kept as a string, no .gdshader file) paints a gradient from the zenith to a hazy
/// horizon, a sun disc with a soft glow, and two layers of drifting clouds. Underground, <see cref="Apply"/> moves the fog and
/// ambient light toward the current floor's theme.
/// </summary>
public static class SkyBuilder
{
    /// <summary>Where the sun's light comes from; the directional light uses the same angles so shadows agree with the disc.</summary>
    public static readonly Vector3 SunRotationDegrees = new(-50, 35, 0);

    private const string SkyCode = """
shader_type sky;

uniform vec3 zenith_color = vec3(0.22, 0.45, 0.86);
uniform vec3 mid_color = vec3(0.45, 0.68, 0.94);
uniform vec3 horizon_color = vec3(0.78, 0.86, 0.94);
uniform vec3 ground_color = vec3(0.30, 0.34, 0.38);
uniform vec3 sun_dir = vec3(0.0, 0.8, 0.6);
uniform vec3 sun_color = vec3(1.0, 0.93, 0.78);
uniform float cloud_cover = 0.5;
uniform float cloud_speed = 0.006;

float hash21(vec2 p) {
	p = fract(p * vec2(123.34, 456.21));
	p += dot(p, p + 45.32);
	return fract(p.x * p.y);
}

float vnoise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	float a = hash21(i);
	float b = hash21(i + vec2(1.0, 0.0));
	float c = hash21(i + vec2(0.0, 1.0));
	float d = hash21(i + vec2(1.0, 1.0));
	return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}

float fbm(vec2 p) {
	float v = 0.0;
	float a = 0.5;
	for (int i = 0; i < 4; i++) {
		v += a * vnoise(p);
		p = p * 2.03 + vec2(17.0, 9.0);
		a *= 0.5;
	}
	return v;
}

void sky() {
	vec3 dir = normalize(EYEDIR);
	float h = dir.y;

	// Gradient: deep blue overhead, paler toward the horizon, then the ground haze below it.
	vec3 col;
	if (h >= 0.0) {
		col = mix(horizon_color, mid_color, smoothstep(0.0, 0.22, h));
		col = mix(col, zenith_color, smoothstep(0.18, 0.85, h));
	} else {
		col = mix(horizon_color, ground_color, smoothstep(0.0, 0.35, -h));
	}

	if (!AT_CUBEMAP_PASS) {
		float sd = max(dot(dir, normalize(sun_dir)), 0.0);

		// Warm glow around the sun, then a crisp disc.
		col += sun_color * (pow(sd, 6.0) * 0.12 + pow(sd, 48.0) * 0.30);
		float disc = smoothstep(0.99925, 0.99965, sd);
		col = mix(col, sun_color * 1.25, disc);

		// Clouds on a flat layer overhead: a broad bank and a finer wisp layer, drifting with the wind.
		if (h > 0.015) {
			vec2 uv = dir.xz / (h + 0.18);
			float t = TIME * cloud_speed;
			float bank = fbm(uv * 0.9 + vec2(t * 2.0, t));
			float wisp = fbm(uv * 2.6 + vec2(t * 5.0, -t * 2.0) + 11.0);
			float dens = bank * 0.78 + wisp * 0.22;
			float c = smoothstep(1.0 - cloud_cover, 1.0 - cloud_cover + 0.16, dens);

			// Light each cloud from the sun's side: thinner edges toward the sun are brighter, the far side greyer.
			float lit = fbm(uv * 0.9 + vec2(t * 2.0, t) + sun_dir.xz * 0.12);
			float shade = clamp(0.62 + (bank - lit) * 3.0, 0.0, 1.0);
			vec3 cloud_col = mix(vec3(0.62, 0.68, 0.78), vec3(1.0, 0.99, 0.97), shade);
			cloud_col += sun_color * pow(sd, 10.0) * 0.18;

			c *= smoothstep(0.015, 0.20, h);
			col = mix(col, cloud_col, c * 0.92);
		}
	}

	COLOR = col;
}
""";

    private static Shader? _shader;

    /// <summary>Basis of the sun: it shines along -Z, so the sun sits toward +Z.</summary>
    public static Vector3 SunDirection()
    {
        var b = Basis.FromEuler(new Vector3(Mathf.DegToRad(SunRotationDegrees.X), Mathf.DegToRad(SunRotationDegrees.Y), Mathf.DegToRad(SunRotationDegrees.Z)));
        return b.Z.Normalized();
    }

    public static Godot.Environment Create(out ShaderMaterial skyMaterial)
    {
        _shader ??= new Shader { Code = SkyCode };
        skyMaterial = new ShaderMaterial { Shader = _shader };
        skyMaterial.SetShaderParameter("sun_dir", SunDirection());

        var s = DungeonThemes.Surface;
        return new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = new Sky { SkyMaterial = skyMaterial },
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(s.AmbientR, s.AmbientG, s.AmbientB),
            AmbientLightEnergy = s.AmbientEnergy,
            FogEnabled = true,
            FogLightColor = new Color(s.FogR, s.FogG, s.FogB),
            FogDensity = s.FogDensity,
            FogSkyAffect = 0f, // the sky shader paints its own haze at the horizon
        };
    }

    /// <summary>Sets the fog and ambient light from an atmosphere.</summary>
    public static void Apply(Godot.Environment env, Atmosphere a)
    {
        env.FogLightColor = new Color(a.FogR, a.FogG, a.FogB);
        env.FogDensity = a.FogDensity;
        env.AmbientLightColor = new Color(a.AmbientR, a.AmbientG, a.AmbientB);
        env.AmbientLightEnergy = a.AmbientEnergy;
    }
}
