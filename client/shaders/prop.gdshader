// Props: a flat colour with a model-aligned pixel detail texture, and optional sway for leaves.
shader_type spatial;
render_mode cull_disabled;

uniform vec3 tint : source_color = vec3(1.0);
uniform sampler2D detail : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform float detail_scale = 0.5;
uniform float sway = 0.0;
// Night glow for window panes and lamps: 0 by day, 1 at night. Panes light in about two houses of three.
uniform float glow = 0.0;
uniform float glow_some = 0.0;
uniform vec3 glow_color : source_color = vec3(1.0, 0.72, 0.36);

varying vec3 world_position;
varying vec3 world_normal;

void vertex() {
	vec3 world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	float wave = sin(TIME * 1.7 + world.x * 0.35 + world.z * 0.27);
	float flutter = sin(TIME * 4.3 + world.x * 1.3 + world.y * 1.9);
	VERTEX.x += (wave * 0.08 + flutter * 0.03) * sway;
	VERTEX.z += (wave * 0.05 - flutter * 0.03) * sway;
	// The texture is laid in the model's own axes, so the courses of a turned house still run
	// along its walls; the model's position shifts it, so neighbours do not repeat each other.
	world_position = VERTEX * length(MODEL_MATRIX[0].xyz) + MODEL_MATRIX[3].xyz;
	world_normal = abs(normalize(NORMAL));
}

void fragment() {
	// Leaves between the camera and the player dissolve, so a tree crown never blinds the view.
	if (sway > 0.5) {
		// A clean cut, not a dither: at the pixel resolution a dither shows as a coarse checkerboard.
		if (-VERTEX.z < 2.2) {
			discard;
		}
	}
	vec3 blend = world_normal / (world_normal.x + world_normal.y + world_normal.z);
	vec3 p = world_position * detail_scale;
	vec3 sample_color = texture(detail, p.zy).rgb * blend.x + texture(detail, p.xz).rgb * blend.y
		+ texture(detail, p.xy).rgb * blend.z;
	ALBEDO = tint * sample_color;
	// Light passes through leaves, so their shaded side stays green instead of going black.
	EMISSION = ALBEDO * 0.26 * step(0.5, sway);
	if (glow > 0.0) {
		vec2 cell = floor(MODEL_MATRIX[3].xz * 0.37);
		float lit = mix(1.0, step(0.34, fract(sin(dot(cell, vec2(12.9898, 78.233))) * 43758.5453)), glow_some);
		EMISSION += glow_color * glow * lit * 1.8;
		ALBEDO = mix(ALBEDO, glow_color, glow * lit * 0.6);
	}
	ROUGHNESS = 1.0;
	SPECULAR = 0.1;
}
