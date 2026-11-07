// Full-screen pass: 1 px outlines from depth and normals, then a fixed palette with Bayer dither.
shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never, depth_test_disabled, fog_disabled;

uniform sampler2D screen_tex : hint_screen_texture, repeat_disable, filter_nearest;
uniform sampler2D depth_tex : hint_depth_texture, repeat_disable, filter_nearest;
uniform sampler2D normal_tex : hint_normal_roughness_texture, repeat_disable, filter_nearest;
uniform vec3 palette[128];
uniform int palette_size = 32;
uniform vec3 outline_color;
uniform float dither_amount = 0.012;
uniform float palette_strength = 1.0;
uniform float normal_threshold = 0.75;

const float BAYER[16] = float[16](0.0, 8.0, 2.0, 10.0, 12.0, 4.0, 14.0, 6.0,
                                  3.0, 11.0, 1.0, 9.0, 15.0, 7.0, 13.0, 5.0);

vec3 to_srgb(vec3 c) {
	return mix(c * 12.92, 1.055 * pow(max(c, vec3(0.0)), vec3(1.0 / 2.4)) - 0.055, step(0.0031308, c));
}

vec3 to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c));
}

float view_depth(vec2 uv, mat4 inv_projection) {
	vec4 p = inv_projection * vec4(uv * 2.0 - 1.0, texture(depth_tex, uv).r, 1.0);
	return -p.z / p.w;
}

void vertex() {
	POSITION = vec4(VERTEX.xy, 1.0, 1.0);
}

void fragment() {
	vec2 px = 1.0 / VIEWPORT_SIZE;
	vec2 offsets[4] = vec2[4](vec2(px.x, 0.0), vec2(-px.x, 0.0), vec2(0.0, px.y), vec2(0.0, -px.y));
	float depth = view_depth(SCREEN_UV, INV_PROJECTION_MATRIX);
	vec4 normal_sample = texture(normal_tex, SCREEN_UV);
	vec3 normal = normal_sample.xyz * 2.0 - 1.0;
	bool leaves = normal_sample.a < 0.8;

	float depth_edge = 0.0;
	float normal_gap = 0.0;
	float normal_edge = 0.0;
	for (int i = 0; i < 4; i++) {
		vec2 uv = SCREEN_UV + offsets[i];
		float other = view_depth(uv, INV_PROJECTION_MATRIX);
		// Only the nearer pixel draws the line, so the outline stays 1 px wide.
		depth_edge = max(depth_edge, step(depth * 0.04 + 0.08, other - depth));
		vec3 other_normal = texture(normal_tex, uv).xyz * 2.0 - 1.0;
		float facing = dot(normal, other_normal);
		normal_edge = max(normal_edge, step(facing, normal_threshold));
		normal_gap = max(normal_gap, step(facing, 0.97));
	}

	vec3 color = to_srgb(texture(screen_tex, SCREEN_UV).rgb);
	if (depth < 1000.0) {
		color *= 1.0 + normal_edge * 0.06;
	}
	ivec2 cell = ivec2(FRAGCOORD.xy) % 4;
	color += (BAYER[cell.y * 4 + cell.x] / 16.0 - 0.5) * dither_amount;

	vec3 best = palette[0];
	float best_distance = 1000.0;
	for (int i = 0; i < palette_size; i++) {
		vec3 delta = color - palette[i];
		float distance_squared = dot(delta, delta);
		if (distance_squared < best_distance) {
			best_distance = distance_squared;
			best = palette[i];
		}
	}
	// Grass shares the normal of the ground, so a depth step alone draws no line around a blade.
	vec3 final_color = mix(color, best, palette_strength);
	if (depth_edge > 0.5 && normal_gap > 0.5 && depth < 1000.0 && !leaves) {
		final_color = mix(final_color, outline_color, 0.72);
	}
	ALBEDO = to_linear(final_color);
}
